using System;

namespace backend.DbModel
{
    public class WorkSession
    {
        public int Id { get; set; }
        // Public session identifier shared with client (GUID string)
        public string SessionId { get; set; } = string.Empty;
        public int? WorkHourId { get; set; }
        public string? WorkerName { get; set; }
        public string? SerialNo { get; set; }
        public string? ProcessName { get; set; }
        // When session originally started on server
        public DateTime StartTimeActual { get; set; } = DateTime.Now;
        // Last heartbeat timestamp
        public DateTime LastHeartbeat { get; set; } = DateTime.Now;
        // Elapsed seconds reported by client (snapshot)
        public int ElapsedSeconds { get; set; }
    // The state of timer: "active" or "paused"
        public string? ActiveClock { get; set; }
        // Optional JSON store for NCM rows or other metadata
        public string? MetadataJson { get; set; }
        // Session state: NotStarted, Working, Completed
        public string? State { get; set; }
    }
}
