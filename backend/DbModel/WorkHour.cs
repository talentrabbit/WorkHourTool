using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.DbModel
{
    [Table("WorkHours")]
    public class WorkHour
    {
        public int Id { get; set; }
        public string? WorkerName { get; set; }
        public string? ProcessName { get; set; }
        public double EffectiveHours { get; set; }
        // Planned hours set during task creation/assignment
        public double PlannedHours { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        // State of the work hour: NotStarted, Working, Completed, etc.
        public string State { get; set; } = "NotStarted";

        // Actual start/end times which may differ from scheduled StartTime/EndTime
        public DateTime? StartTimeActual { get; set; }
        public DateTime? EndTimeActual { get; set; }
    }
}
