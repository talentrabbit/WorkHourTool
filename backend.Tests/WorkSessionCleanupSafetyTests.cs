using System.Text.Json;
using backend.Controllers;
using backend.Data;
using backend.DbModel;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests;

// Each test owns its SQLite database and manager; no cleanup service or production data is used.
public class WorkSessionCleanupSafetyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;
    private readonly AppDbContext _db;
    private readonly WorkSessionManager _manager;
    private readonly SaveCounter _saves = new();
    private static readonly DateTime EndTime = new(2026, 11, 11, 23, 30, 0);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WorkSessionCleanupSafetyTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseSqlite(_connection).AddInterceptors(_saves))
            .BuildServiceProvider();
        _db = _services.GetRequiredService<AppDbContext>();
        _manager = new WorkSessionManager(NullLogger<WorkSessionManager>.Instance,
            _services.GetRequiredService<IServiceScopeFactory>());
    }

    [Theory]
    [InlineData("Expired", true)]
    [InlineData("Expired", false)]
    [InlineData("Completed", true)]
    [InlineData("Completed", false)]
    public void TerminalHeartbeat_ReturnsCanonicalSnapshotWithoutWrites(string state, bool useManager)
    {
        var session = Seed(state);
        var before = Snapshot();
        var saves = _saves.Count;

        var result = Controller(useManager).SessionHeartbeat(new WorkHoursController.SessionHeartbeatRequest
        {
            SessionId = session.SessionId,
            ElapsedSeconds = 99999,
            State = "Working",
            ActiveClock = "active",
            MetadataJson = "{\"stale\":true}"
        });

        AssertCanonical(Body(result), session);
        Assert.Equal(before, Snapshot());
        Assert.Equal(saves, _saves.Count);
    }

    [Theory]
    [InlineData("Expired", false)]
    [InlineData("Expired", true)]
    [InlineData("Completed", false)]
    [InlineData("Completed", true)]
    public void ManagerHeartbeat_DoesNotReviveTerminalSession(string state, bool staleMemory)
    {
        var session = Seed(staleMemory ? "Working" : state);
        if (staleMemory)
        {
            Manage(session);
            // Simulate a cleanup commit while memory still holds an older snapshot.
            SetTerminal(session.SessionId, state);
        }
        var expected = _db.WorkSessions.AsNoTracking().Single();
        var before = Snapshot();
        var saves = _saves.Count;

        var info = _manager.Heartbeat(new WorkSessionManager.ManagerHeartbeatRequest
        {
            SessionId = session.SessionId,
            ElapsedSeconds = 99999,
            State = "Working",
            ActiveClock = "active",
            MetadataJson = "{\"stale\":true}"
        });

        Assert.NotNull(info);
        AssertCanonical(JsonSerializer.SerializeToElement(info, JsonOptions), expected);
        Assert.Null(_manager.GetByWorkHourId(session.WorkHourId!.Value));
        Assert.Equal(state, _manager.GetBySessionId(session.SessionId)!.State);
        Assert.Equal(before, Snapshot());
        Assert.Equal(saves, _saves.Count);
    }

    [Theory]
    [InlineData("Expired")]
    [InlineData("Completed")]
    public void ManagerResume_DoesNotReviveTerminalSession(string state)
    {
        var session = Seed(state);
        var before = Snapshot();
        var saves = _saves.Count;

        var info = _manager.StartOrResume(new WorkSessionManager.ManagerStartRequest
        {
            SessionId = session.SessionId,
            ElapsedSeconds = 99999,
            State = "Working",
            ActiveClock = "active"
        });

        AssertCanonical(JsonSerializer.SerializeToElement(info, JsonOptions), session);
        Assert.Equal(before, Snapshot());
        Assert.Equal(saves, _saves.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetSession_ReturnsLatestExpiredSnapshotAndEndTime(bool useManager)
    {
        var session = Seed("Expired");
        _db.WorkSessions.Add(new WorkSession
        {
            SessionId = "older-session",
            WorkHourId = session.WorkHourId,
            State = "Completed",
            ElapsedSeconds = 10,
            LastHeartbeat = session.LastHeartbeat.AddHours(-1)
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        var before = Snapshot();
        var saves = _saves.Count;

        var body = Body(Controller(useManager).GetSessionByWorkHour(session.WorkHourId!.Value));

        AssertCanonical(body, session);
        Assert.Equal(EndTime, body.GetProperty("endTimeActual").GetDateTime());
        Assert.Equal(session.MetadataJson, body.GetProperty("metadataJson").GetString());
        Assert.Equal(session.LastHeartbeat, body.GetProperty("lastHeartbeat").GetDateTime());
        Assert.Equal(before, Snapshot());
        Assert.Equal(saves, _saves.Count);
    }

    [Fact]
    public void GetSession_IgnoresStaleManagedSessionAfterCleanup()
    {
        var session = Seed("Working");
        Manage(session);
        SetTerminal(session.SessionId, "Expired");

        var body = Body(Controller().GetSessionByWorkHour(session.WorkHourId!.Value));

        Assert.Equal("Expired", body.GetProperty("state").GetString());
        Assert.Equal(3600, body.GetProperty("elapsedSeconds").GetInt32());
        Assert.Equal("paused", body.GetProperty("activeClock").GetString());
        Assert.Equal(EndTime, body.GetProperty("endTimeActual").GetDateTime());
    }

    [Fact]
    public void GetSession_ReturnsNotFoundOnlyWhenNoSessionExists()
    {
        var session = Seed("Expired");
        _db.WorkSessions.Remove(_db.WorkSessions.Single());
        _db.SaveChanges();

        Assert.IsType<NotFoundResult>(Controller().GetSessionByWorkHour(session.WorkHourId!.Value));
    }

    [Fact]
    public void CompleteWorkHour_RejectsCleanupCompletedWorkHourWithoutOverwritingFields()
    {
        var session = Seed("Expired");
        var before = Snapshot();
        var saves = _saves.Count;

        var result = Controller().CompleteWorkHour(new WorkHoursController.CompleteWorkHourRequest
        {
            WorkHourId = session.WorkHourId!.Value,
            EffectiveHours = 99,
            WorkerName = "Stale browser"
        });

        Assert.Equal(409, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(before, Snapshot());
        Assert.Equal(saves, _saves.Count);
    }

    [Fact]
    public void CompleteWorkHour_RejectsRepeatCompletionWithoutOverwritingFirstEndTime()
    {
        var session = Seed("Working");
        var request = new WorkHoursController.CompleteWorkHourRequest
        {
            WorkHourId = session.WorkHourId!.Value,
            EffectiveHours = 1.25,
            WorkerName = "Original worker"
        };
        Assert.IsType<OkObjectResult>(Controller().CompleteWorkHour(request));
        var before = Snapshot();
        var saves = _saves.Count;
        request.EffectiveHours = 99;
        request.WorkerName = "Stale browser";

        Assert.Equal(409, Assert.IsType<ConflictObjectResult>(Controller().CompleteWorkHour(request)).StatusCode);
        Assert.Equal(before, Snapshot());
        Assert.Equal(saves, _saves.Count);
    }

    [Fact]
    public void SetWorkingById_RejectsCompletedWorkHourWithoutWrites()
    {
        var session = Seed("Expired");
        var before = Snapshot();
        var saves = _saves.Count;

        var result = Controller().SetWorkingById(new WorkHoursController.SetWorkingByIdRequest
        {
            WorkHourId = session.WorkHourId,
            WorkerName = "Stale browser"
        });

        Assert.Equal(409, Assert.IsType<ConflictObjectResult>(result).StatusCode);
        Assert.Equal(before, Snapshot());
        Assert.Equal(saves, _saves.Count);
    }

    [Fact]
    public void ActiveHeartbeatAndGet_StillReturnLiveManagerElapsed()
    {
        var session = Seed("Working");
        var body = Body(Controller().SessionHeartbeat(new WorkHoursController.SessionHeartbeatRequest
        {
            SessionId = session.SessionId,
            ElapsedSeconds = 4000,
            ActiveClock = "paused",
            State = "Working"
        }));

        Assert.Equal(4000, body.GetProperty("elapsedSeconds").GetInt32());
        Assert.Equal("Working", body.GetProperty("state").GetString());
        Assert.Equal(4000, Body(Controller().GetSessionByWorkHour(session.WorkHourId!.Value))
            .GetProperty("elapsedSeconds").GetInt32());
    }

    private WorkHoursController Controller(bool useManager = true) =>
        new(NullLogger<WorkHoursController>.Instance, useManager ? _manager : null!, _db);

    private WorkSession Seed(string state)
    {
        var workHour = new WorkHour
        {
            Product = new Product { SerialNo = "SN-CLEANUP-SAFETY" },
            WorkerName = "Original worker",
            ProcessName = "Assembly",
            State = state == "Working" ? "Working" : "Completed",
            EffectiveHours = 1,
            StartTime = EndTime.AddHours(-2),
            EndTime = EndTime,
            StartTimeActual = EndTime.AddHours(-1),
            EndTimeActual = state == "Working" ? null : EndTime
        };
        _db.WorkHours.Add(workHour);
        _db.SaveChanges();
        var session = new WorkSession
        {
            SessionId = "cleanup-session",
            WorkHourId = workHour.Id,
            WorkerName = workHour.WorkerName,
            ProcessName = workHour.ProcessName,
            State = state,
            ActiveClock = "paused",
            ElapsedSeconds = 3600,
            StartTimeActual = EndTime.AddHours(-1),
            LastHeartbeat = EndTime.AddMinutes(-5),
            MetadataJson = "{\"ncmRows\":[]}"
        };
        _db.WorkSessions.Add(session);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return session;
    }

    private void Manage(WorkSession session) => _manager.StartOrResume(new WorkSessionManager.ManagerStartRequest
    {
        SessionId = session.SessionId,
        WorkHourId = session.WorkHourId,
        ActiveClock = "paused",
        State = "Working"
    });

    private void SetTerminal(string sessionId, string state)
    {
        var session = _db.WorkSessions.Single(s => s.SessionId == sessionId);
        session.State = state;
        session.ActiveClock = "paused";
        session.ElapsedSeconds = 3600;
        session.LastHeartbeat = EndTime.AddMinutes(-5);
        var workHour = _db.WorkHours.Single();
        workHour.State = "Completed";
        workHour.EndTimeActual = EndTime;
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private string Snapshot() => JsonSerializer.Serialize(new
    {
        Sessions = _db.WorkSessions.AsNoTracking().OrderBy(s => s.Id).ToArray(),
        WorkHours = _db.WorkHours.AsNoTracking().OrderBy(w => w.Id).ToArray(),
        NcmTimes = _db.NcmTimes.AsNoTracking().OrderBy(n => n.Id).ToArray()
    });

    private static JsonElement Body(IActionResult result) =>
        JsonSerializer.SerializeToElement(Assert.IsType<OkObjectResult>(result).Value, JsonOptions);

    private static void AssertCanonical(JsonElement body, WorkSession expected)
    {
        Assert.Equal(expected.SessionId, body.GetProperty("sessionId").GetString());
        Assert.Equal(expected.State, body.GetProperty("state").GetString());
        Assert.Equal(expected.ElapsedSeconds, body.GetProperty("elapsedSeconds").GetInt32());
        Assert.Equal(expected.ActiveClock, body.GetProperty("activeClock").GetString());
    }

    public void Dispose()
    {
        _manager.Dispose();
        _services.Dispose();
        _connection.Dispose();
    }

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Count++;
            return result;
        }
    }
}