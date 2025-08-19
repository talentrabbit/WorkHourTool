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
        public double NcmHour => (EndTime - StartTime).TotalHours;
    }
}
