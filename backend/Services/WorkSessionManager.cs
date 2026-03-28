using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using backend.DbModel;
using backend.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Services
{
    // Long-running in-memory manager for active WorkSession instances.
    // Each managed session has its own timer which increments elapsed seconds
    // and periodically persists a snapshot to the database.
    public class WorkSessionManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, ManagedSession> _sessions = new();
        private readonly ILogger<WorkSessionManager> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        // How often (seconds) to persist session state to DB from the timer
        private const int SAVE_INTERVAL_SECONDS = 15;

        public WorkSessionManager(ILogger<WorkSessionManager> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            // Log startup so we can verify messages appear in the same Serilog file as controllers
            _logger.LogInformation("WorkSessionManager initialized");
        }

        // Lightweight request DTO used by the manager to avoid coupling to controller types
        public class ManagerStartRequest
        {
            public string? SessionId { get; set; }
            public int? WorkHourId { get; set; }
            public string? WorkerName { get; set; }
            public string? SerialNo { get; set; }
            public string? ProcessName { get; set; }
            public int? ElapsedSeconds { get; set; }
            public string? ActiveClock { get; set; }
            public string? MetadataJson { get; set; }
            public string? State { get; set; }
        }

        public class ManagerHeartbeatRequest
        {
            public string? SessionId { get; set; }
            public int? ElapsedSeconds { get; set; }
            public string? ActiveClock { get; set; }
            public string? MetadataJson { get; set; }
            public string? State { get; set; }
        }

        public ManagedSessionInfo StartOrResume(ManagerStartRequest req)
        {
            // Try to find an open session in DB first (by WorkHourId if provided)
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                WorkSession session = null;
                if (req.WorkHourId.HasValue)
                {
                    session = db.WorkSessions
                        .Where(s => s.WorkHourId == req.WorkHourId.Value)
                        .OrderByDescending(s => s.LastHeartbeat)
                        .FirstOrDefault(s => s.State != "Completed" && s.State != "Expired");
                }

                if (session == null && !string.IsNullOrWhiteSpace(req.SessionId))
                {
                    session = db.WorkSessions.FirstOrDefault(s => s.SessionId == req.SessionId);
                }

                if (session == null)
                {
                    // Create new session
                    session = new WorkSession
                    {
                        SessionId = string.IsNullOrWhiteSpace(req.SessionId) ? Guid.NewGuid().ToString() : req.SessionId,
                        WorkHourId = req.WorkHourId,
                        WorkerName = req.WorkerName,
                        SerialNo = req.SerialNo,
                        ProcessName = req.ProcessName,
                        StartTimeActual = DateTime.Now,
                        LastHeartbeat = DateTime.Now,
                        ElapsedSeconds = req.ElapsedSeconds ?? 0,
                        ActiveClock = req.ActiveClock,
                        MetadataJson = req.MetadataJson,
                        State = string.IsNullOrWhiteSpace(req.State) ? "Working" : req.State
                    };

                    db.WorkSessions.Add(session);
                    db.SaveChanges();
                }

                // Ensure we have a managed instance
                var managed = _sessions.GetOrAdd(session.SessionId, sid => new ManagedSession(session, this, _logger));

                // If incoming request has larger elapsedSeconds, prefer it
                if (req.ElapsedSeconds.HasValue && req.ElapsedSeconds.Value > managed.Session.ElapsedSeconds)
                {
                    managed.SetElapsed(req.ElapsedSeconds.Value);
                }

                managed.UpdateFromRequest(req);
                managed.EnsureTimerStarted();

                return new ManagedSessionInfo { SessionId = managed.Session.SessionId, ElapsedSeconds = managed.Session.ElapsedSeconds, State = managed.Session.State };
            }
        }

        public ManagedSessionInfo? Heartbeat(ManagerHeartbeatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.SessionId)) return null;
            if (_sessions.TryGetValue(req.SessionId, out var managed))
            {
                managed.OnHeartbeat(req);
                return new ManagedSessionInfo { SessionId = managed.Session.SessionId, ElapsedSeconds = managed.Session.ElapsedSeconds, State = managed.Session.State };
            }

            // If not in memory, try to load from DB and create managed session
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var session = db.WorkSessions.FirstOrDefault(s => s.SessionId == req.SessionId);
                if (session == null) return null;
                managed = _sessions.GetOrAdd(session.SessionId, sid => new ManagedSession(session, this, _logger));
                managed.OnHeartbeat(req);
                managed.EnsureTimerStarted();
                return new ManagedSessionInfo { SessionId = managed.Session.SessionId, ElapsedSeconds = managed.Session.ElapsedSeconds, State = managed.Session.State };
            }
        }

        public WorkSession? GetBySessionId(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return null;
            if (_sessions.TryGetValue(sessionId, out var m)) return m.Session;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return db.WorkSessions.AsNoTracking().FirstOrDefault(s => s.SessionId == sessionId);
            }
        }

        public WorkSession? GetByWorkHourId(int workHourId)
        {
            // check managed sessions first
            var managed = _sessions.Values.FirstOrDefault(m => m.Session.WorkHourId == workHourId && m.Session.State != "Completed" && m.Session.State != "Expired");
            if (managed != null) return managed.Session;

            // fallback to DB
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                return db.WorkSessions.AsNoTracking()
                    .Where(s => s.WorkHourId == workHourId)
                    .OrderByDescending(s => s.LastHeartbeat)
                    .FirstOrDefault(s => s.State != "Completed" && s.State != "Expired");
            }
        }

        public bool PauseSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;
            if (_sessions.TryGetValue(sessionId, out var managed))
            {
                managed.Session.ActiveClock = "paused";
                return true;
            }
            return false;
        }

        public bool CompleteSession(string sessionId, int? finalElapsedSeconds = null)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;
            if (_sessions.TryRemove(sessionId, out var managed))
            {
                managed.StopAndComplete(finalElapsedSeconds);
                return true;
            }

            // Not in memory: try to mark completed in DB
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var session = db.WorkSessions.FirstOrDefault(s => s.SessionId == sessionId);
                if (session == null) return false;
                if (finalElapsedSeconds.HasValue && finalElapsedSeconds.Value > session.ElapsedSeconds) session.ElapsedSeconds = finalElapsedSeconds.Value;
                session.LastHeartbeat = DateTime.Now;
                session.State = "Completed";

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

                // Optionally persist NCM rows
                if (!string.IsNullOrWhiteSpace(session.MetadataJson))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(session.MetadataJson);
                        if (doc.RootElement.TryGetProperty("ncmRows", out var ncmRows) && ncmRows.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            int productId = 0;
                            if (session.WorkHourId.HasValue)
                            {
                                productId = db.WorkHours.Where(w => w.Id == session.WorkHourId.Value).Select(w => w.ProductId).FirstOrDefault();
                            }

                            foreach (var el in ncmRows.EnumerateArray())
                            {
                                var pe = el.TryGetProperty("processEngineer", out var peEl) && peEl.ValueKind == System.Text.Json.JsonValueKind.String ? peEl.GetString() : null;
                                var callingContent = el.TryGetProperty("callingContent", out var ccEl) && ccEl.ValueKind == System.Text.Json.JsonValueKind.String ? ccEl.GetString() : null;
                                double nh = 0;
                                if (el.TryGetProperty("ncmHours", out var nhEl) && nhEl.ValueKind == System.Text.Json.JsonValueKind.Number) nh = nhEl.GetDouble();

                                var ncm = new NcmTime
                                {
                                    ProcessEngineer = pe,
                                    ProcessName = session.ProcessName,
                                    StartTime = DateTime.Now,
                                    EndTime = DateTime.Now,
                                    NcmHours = nh,
                                    ProductId = productId,
                                    State = "Notified",
                                    CallingContent = callingContent
                                };
                                db.NcmTimes.Add(ncm);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to parse MetadataJson for session {SessionId}", session.SessionId);
                    }
                }

                db.SaveChanges();
                return true;
            }
        }

        internal void PersistSnapshot(WorkSession session)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var existing = db.WorkSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
                if (existing != null)
                {
                    existing.LastHeartbeat = session.LastHeartbeat;
                    existing.ElapsedSeconds = session.ElapsedSeconds;
                    existing.ActiveClock = session.ActiveClock;
                    existing.MetadataJson = session.MetadataJson;
                    existing.State = session.State;
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to persist WorkSession snapshot for {SessionId}", session.SessionId);
            }
        }

        public void Dispose()
        {
            _logger?.LogInformation("WorkSessionManager disposing, will stop {Count} managed session(s)", _sessions.Count);
            foreach (var kv in _sessions)
            {
                try { kv.Value.Dispose(); } catch { }
            }
            _sessions.Clear();
            _logger?.LogInformation("WorkSessionManager disposed");
        }

        // Minimal DTO to return basic info
        public class ManagedSessionInfo
        {
            public string SessionId { get; set; } = string.Empty;
            public int ElapsedSeconds { get; set; }
            public string? State { get; set; }
        }

        // Internal managed session with its own timer
        public class ManagedSession : IDisposable
        {
            private readonly WorkSessionManager _owner;
            private readonly ILogger _logger;
            private readonly object _lock = new object();
            private Timer? _timer;
            private int _secondsSinceLastSave = 0;

            public WorkSession Session { get; private set; }

            public ManagedSession(WorkSession session, WorkSessionManager owner, ILogger logger)
            {
                Session = session;
                _owner = owner;
                _logger = logger;
            }

            public void UpdateFromRequest(ManagerStartRequest req)
            {
                lock (_lock)
                {
                    Session.WorkHourId = req.WorkHourId ?? Session.WorkHourId;
                    Session.WorkerName = req.WorkerName ?? Session.WorkerName;
                    Session.SerialNo = req.SerialNo ?? Session.SerialNo;
                    Session.ProcessName = req.ProcessName ?? Session.ProcessName;
                    Session.ActiveClock = req.ActiveClock ?? Session.ActiveClock;
                    Session.MetadataJson = req.MetadataJson ?? Session.MetadataJson;
                    Session.State = string.IsNullOrWhiteSpace(req.State) ? Session.State : req.State;
                    Session.LastHeartbeat = DateTime.Now;
                }
            }

            public void OnHeartbeat(ManagerHeartbeatRequest req)
            {
                lock (_lock)
                {
                    Session.LastHeartbeat = DateTime.Now;
                    if (req.ElapsedSeconds.HasValue && req.ElapsedSeconds.Value > Session.ElapsedSeconds)
                        Session.ElapsedSeconds = req.ElapsedSeconds.Value;
                    Session.ActiveClock = req.ActiveClock ?? Session.ActiveClock;
                    Session.MetadataJson = req.MetadataJson ?? Session.MetadataJson;
                    Session.State = req.State ?? Session.State;
                }
            }

            public void SetElapsed(int seconds)
            {
                lock (_lock) Session.ElapsedSeconds = seconds;
            }

            public void SetState(string state)
            {
                lock (_lock)
                {
                    Session.State = state;
                }
            }

            public void EnsureTimerStarted()
            {
                lock (_lock)
                {
                    if (_timer != null) return;
                    // tick every 1s
                    _timer = new Timer(Tick, null, 1000, 1000);
                }
            }

            private void Tick(object? _) 
            {
                try
                {
                    lock (_lock)
                    {
                        // Only advance elapsed when the active clock is 'active' and state is Working
                        if (string.Equals(Session.ActiveClock, "active", StringComparison.OrdinalIgnoreCase) && string.Equals(Session.State, "Working", StringComparison.OrdinalIgnoreCase))
                        {
                            Session.ElapsedSeconds++;
                        }
                        _secondsSinceLastSave++;

                        if (_secondsSinceLastSave >= SAVE_INTERVAL_SECONDS)
                        {
                            _secondsSinceLastSave = 0;
                            // Persist snapshot asynchronously
                            Task.Run(() => _owner.PersistSnapshot(Session));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in ManagedSession.Tick for {SessionId}", Session.SessionId);
                }
            }

            public void StopAndComplete(int? finalElapsed)
            {
                lock (_lock)
                {
                    if (finalElapsed.HasValue && finalElapsed.Value > Session.ElapsedSeconds) Session.ElapsedSeconds = finalElapsed.Value;
                    Session.State = "Completed";
                    Session.LastHeartbeat = DateTime.Now;
                }

                // persist final snapshot and update workhour/ncm
                _owner.PersistSnapshot(Session);

                try
                {
                    using var scope = _owner._scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    // Note: WorkSession snapshot has already been persisted above by PersistSnapshot(Session).
                    // The following block intentionally avoids repeating WorkSession field updates and
                    // focuses only on WorkHour and NCM persistence.

                    if (Session.WorkHourId.HasValue)
                    {
                        var wh = db.WorkHours.FirstOrDefault(w => w.Id == Session.WorkHourId.Value);
                        if (wh != null)
                        {
                            wh.EffectiveHours = Math.Round((Session.ElapsedSeconds / 3600.0) * 100) / 100;
                            wh.EndTimeActual = DateTime.Now;
                            wh.State = "Completed";
                            if (!string.IsNullOrWhiteSpace(Session.WorkerName)) wh.WorkerName = Session.WorkerName;
                        }
                    }

                    // optionally persist ncm rows
                    if (!string.IsNullOrWhiteSpace(Session.MetadataJson))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(Session.MetadataJson);
                            if (doc.RootElement.TryGetProperty("ncmRows", out var ncmRows) && ncmRows.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                int productId = 0;
                                if (Session.WorkHourId.HasValue)
                                {
                                    productId = db.WorkHours.Where(w => w.Id == Session.WorkHourId.Value).Select(w => w.ProductId).FirstOrDefault();
                                }

                                foreach (var el in ncmRows.EnumerateArray())
                                {
                                    var pe = el.TryGetProperty("processEngineer", out var peEl) && peEl.ValueKind == System.Text.Json.JsonValueKind.String ? peEl.GetString() : null;
                                    var callingContent = el.TryGetProperty("callingContent", out var ccEl) && ccEl.ValueKind == System.Text.Json.JsonValueKind.String ? ccEl.GetString() : null;
                                    double nh = 0;
                                    if (el.TryGetProperty("ncmHours", out var nhEl) && nhEl.ValueKind == System.Text.Json.JsonValueKind.Number) nh = nhEl.GetDouble();

                                    var ncm = new NcmTime
                                    {
                                        ProcessEngineer = pe,
                                        ProcessName = Session.ProcessName,
                                        StartTime = DateTime.Now,
                                        EndTime = DateTime.Now,
                                        NcmHours = nh,
                                        ProductId = productId,
                                        State = "Notified",
                                        CallingContent = callingContent
                                    };
                                    db.NcmTimes.Add(ncm);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Failed to parse MetadataJson for session {SessionId}", Session.SessionId);
                        }
                    }

                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to persist final session {SessionId}", Session.SessionId);
                }

                Dispose();
            }

            public void Dispose()
            {
                try { _timer?.Dispose(); } catch { }
                _timer = null;
            }
        }

        public bool RemoveSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;
            try
            {
                // If in-memory, dispose and remove
                if (_sessions.TryRemove(sessionId, out var managed))
                {
                    try { managed.Dispose(); } catch { }
                }

                // Remove any DB record for the session
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var s = db.WorkSessions.FirstOrDefault(x => x.SessionId == sessionId);
                    if (s != null)
                    {
                        db.WorkSessions.Remove(s);
                        db.SaveChanges();
                    }
                }
                _logger?.LogInformation("Removed WorkSession {SessionId} from manager and DB", sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to remove WorkSession {SessionId}", sessionId);
                return false;
            }
        }

        public bool RemoveByWorkHourId(int workHourId)
        {
            try
            {
                // find managed session
                var managed = _sessions.Values.FirstOrDefault(m => m.Session.WorkHourId == workHourId);
                if (managed != null)
                {
                    return RemoveSession(managed.Session.SessionId);
                }

                // fallback: delete DB row(s) matching workHourId
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var rows = db.WorkSessions.Where(s => s.WorkHourId == workHourId).ToList();
                    if (rows.Count > 0)
                    {
                        db.WorkSessions.RemoveRange(rows);
                        db.SaveChanges();
                        _logger?.LogInformation("Removed {Count} WorkSession rows for WorkHourId {WorkHourId} from DB", rows.Count, workHourId);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to remove WorkSession by WorkHourId {WorkHourId}", workHourId);
                return false;
            }
        }
    }
}
