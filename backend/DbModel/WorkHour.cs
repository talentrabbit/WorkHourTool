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
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
    }
}
