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

        // Action/description for the NCM time
        public string? NcmAction { get; set; }
    }
}
