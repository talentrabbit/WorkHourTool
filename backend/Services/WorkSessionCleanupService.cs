using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using backend.Data;
using backend.DbModel;

namespace backend.Services
{
    public class WorkSessionCleanupService : BackgroundService
    {
        private readonly ILogger<WorkSessionCleanupService> _logger;
        private readonly WorkSessionManager _manager;
        private readonly IConfiguration _config;

        // defaults
        private bool _enabled = true;
        private bool _restoreOnStartup = true;
        private int _restoreWindowDays = 0; // 0 => today only
        private TimeSpan _scheduleTime = TimeSpan.Zero; // midnight
        private TimeSpan _staleThreshold = TimeSpan.FromHours(8);

        public WorkSessionCleanupService(ILogger<WorkSessionCleanupService> logger, WorkSessionManager manager, IConfiguration config)
        {
            _logger = logger;
            _manager = manager;
            _config = config;

            try
            {
                var section = _config.GetSection("WorkSessionCleanup");
                if (section.Exists())
                {
                    _enabled = section.GetValue<bool>("Enabled", true);
                    _restoreOnStartup = section.GetValue<bool>("RestoreOnStartup", true);
                    _restoreWindowDays = section.GetValue<int>("RestoreWindowDays", 0);
                    var schedule = section.GetValue<string>("ScheduleTime", "00:00");
                    if (TimeSpan.TryParse(schedule, out var st)) _scheduleTime = st;
                    var staleHours = section.GetValue<int>("StaleThresholdHours", 8);
                    _staleThreshold = TimeSpan.FromHours(staleHours);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read WorkSessionCleanup configuration; using defaults");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("WorkSessionCleanupService is disabled via configuration");
                return;
            }

            _logger.LogInformation("WorkSessionCleanupService started (schedule {Schedule}, staleThresholdHours={Hours}, restoreOnStartup={Restore})", _scheduleTime, _staleThreshold.TotalHours, _restoreOnStartup);

            if (_restoreOnStartup)
            {
                try
                {
                    using var db = new AppDbContext();
                    var startDate = DateTime.Today.AddDays(-_restoreWindowDays);
                    var toRestore = db.WorkSessions
                        .Where(s => (s.State == "Working" || s.State == "NotStarted") && s.StartTimeActual >= startDate)
                        .ToList();

                    if (toRestore.Any())
                    {
                        _logger.LogInformation("Restoring {Count} active WorkSession(s) from DB into WorkSessionManager", toRestore.Count);
                        foreach (var session in toRestore)
                        {
                            try
                            {
                                var req = new WorkSessionManager.ManagerStartRequest
                                {
                                    SessionId = session.SessionId,
                                    WorkHourId = session.WorkHourId,
                                    WorkerName = session.WorkerName,
                                    SerialNo = session.SerialNo,
                                    ProcessName = session.ProcessName,
                                    ElapsedSeconds = session.ElapsedSeconds,
                                    ActiveClock = session.ActiveClock.ToLowerInvariant(),
                                    MetadataJson = session.MetadataJson,
                                    State = session.State
                                };

                                var info = _manager.StartOrResume(req);
                                _logger.LogInformation("Restored session {SessionId} into manager (ElapsedSeconds={Elapsed}, State={State})", info.SessionId, info.ElapsedSeconds, info.State);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to restore WorkSession {SessionId}", session.SessionId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error while attempting to restore sessions on startup");
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Compute next scheduled run (today at ScheduleTime if in future, otherwise tomorrow)
                    var now = DateTime.Now;
                    var scheduledToday = DateTime.Today.Add(_scheduleTime);
                    var nextRun = scheduledToday > now ? scheduledToday : scheduledToday.AddDays(1);
                    var delay = nextRun - now;

                    _logger.LogInformation("Next cleanup scheduled at {NextRun} (in {Delay})", nextRun, delay);

                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (TaskCanceledException) { break; }

                    using var db = new AppDbContext();
                    var cutoff = DateTime.Now - _staleThreshold; // sessions with LastHeartbeat older than this are stale
                    _logger.LogInformation("WorkSessionCleanupService running cleanup at {Now}, cutoff {Cutoff}", DateTime.Now, cutoff);

                    var stale = db.WorkSessions
                        .Where(s => (s.State == "Working" || s.State == "NotStarted") && s.LastHeartbeat < cutoff)
                        .ToList();

                    if (stale.Any())
                    {
                        _logger.LogInformation("Found {Count} stale sessions to cleanup", stale.Count);
                        foreach (var session in stale)
                        {
                            try
                            {
                                _logger.LogInformation("Cleaning session {SessionId}", session.SessionId);
                                // mark expired
                                session.State = "Expired";

                                // ensure EF Core tracks the change and will persist it to the database
                                db.WorkSessions.Update(session);

                                // Persist to WorkHour if referenced
                                if (session.WorkHourId.HasValue)
                                {
                                    var wh = db.WorkHours.FirstOrDefault(w => w.Id == session.WorkHourId.Value);
                                    if (wh != null)
                                    {
                                        wh.EffectiveHours = Math.Round((session.ElapsedSeconds / 3600.0) * 100) / 100;
                                        wh.EndTimeActual = DateTime.Now;
                                        wh.State = "Completed";
                                        if (!string.IsNullOrWhiteSpace(session.WorkerName)) wh.WorkerName = session.WorkerName;
                                    }
                                }

                                // Persist NCM rows if metadata contains ncmRows
                                if (!string.IsNullOrWhiteSpace(session.MetadataJson))
                                {
                                    try
                                    {
                                        using var doc = JsonDocument.Parse(session.MetadataJson);
                                        if (doc.RootElement.TryGetProperty("ncmRows", out var ncmRows) && ncmRows.ValueKind == JsonValueKind.Array)
                                        {
                                            int productId = 0;
                                            if (session.WorkHourId.HasValue)
                                            {
                                                productId = db.WorkHours.Where(w => w.Id == session.WorkHourId.Value).Select(w => w.ProductId).FirstOrDefault();
                                            }

                                            foreach (var el in ncmRows.EnumerateArray())
                                            {
                                                var pe = el.TryGetProperty("processEngineer", out var peEl) && peEl.ValueKind == JsonValueKind.String ? peEl.GetString() : null;
                                                var na = el.TryGetProperty("ncmAction", out var naEl) && naEl.ValueKind == JsonValueKind.String ? naEl.GetString() : null;
                                                double nh = 0;
                                                if (el.TryGetProperty("ncmHours", out var nhEl) && nhEl.ValueKind == JsonValueKind.Number) nh = nhEl.GetDouble();

                                                var ncm = new NcmTime
                                                {
                                                    ProcessEngineer = pe,
                                                    ProcessName = session.ProcessName,
                                                    StartTime = DateTime.Now,
                                                    EndTime = DateTime.Now,
                                                    NcmHours = nh,
                                                    ProductId = productId,
                                                    State = "Notified",
                                                    NcmAction = na
                                                };
                                                db.NcmTimes.Add(ncm);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Failed to parse MetadataJson for session {SessionId}", session.SessionId);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to cleanup session {SessionId}", session.SessionId);
                            }
                        }

                        db.SaveChanges();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning WorkSession entries");
                }

                // loop will compute next scheduled run and wait again
            }
            _logger.LogInformation("WorkSessionCleanupService stopping");
        }
    }
}
