using System;

namespace backend.DbModel
{
    public class NcmTime
    {
        public int Id { get; set; }
        public string? ProcessEngineer { get; set; }
        public string? ProcessName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        // allow null when NcmHours not recorded in DB
        public double? NcmHours { get; set; }

        // State of the NcmTime entry: NotStarted, Working, Completed, etc.
        public string State { get; set; } = "NotStarted";

        // Renamed from NcmAction -> CallingContent
        public string? CallingContent { get; set; }

        // New: type/category of the call (e.g., Phone, Email, Meeting)
        public string? CallType { get; set; }

        // New: actions taken or next steps summary
        public string? Actions { get; set; }
    }
}
