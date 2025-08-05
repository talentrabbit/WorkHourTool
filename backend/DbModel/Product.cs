using System.Collections.Generic;

namespace backend.DbModel
{
    public class Product
    {
        public int Id { get; set; }
        public string? ProjectNo { get; set; }
        public string? IvkNo { get; set; }
        public string? ModalityType { get; set; }
        public string? SystemType { get; set; }
        public string? SerialNo { get; set; }
        public string? ProductLine { get; set; }
        public int UnpackageHours { get; set; }
        public int AssemblyHours { get; set; }
        public int DebugHours { get; set; }
        public int ValidationHours { get; set; }
        public int DisassemblyHours { get; set; }
        public int RepackageHours { get; set; }
        public string? WorkingProcess { get; set; }
        public ICollection<WorkHour> WorkHours { get; set; } = new List<WorkHour>();
        public ICollection<NcmTime> NcmTimes { get; set; } = new List<NcmTime>();
    }
}
