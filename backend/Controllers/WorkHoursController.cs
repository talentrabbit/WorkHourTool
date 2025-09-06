using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using backend.DbModel;
using backend.Data;
using System.IO;
using System.Text.Json;
using System.Linq; // added

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkHoursController : ControllerBase
    {
        private readonly ILogger<WorkHoursController> _logger;
        private static string[] _workerNames = new string[0];
        private static string[] _processNames = new string[0];
        private static string[] _processEngineerNames = new string[0];

        static WorkHoursController()
        {
            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "MIProdCommInfo.json");
                if (System.IO.File.Exists(jsonPath))
                {
                    var json = System.IO.File.ReadAllText(jsonPath);
                    var obj = JsonSerializer.Deserialize<MIProdCommInfo>(json);
                    _workerNames = obj?.WorkerNames ?? new string[0];
                    _processNames = obj?.ProcessNames ?? new string[0];
                    _processEngineerNames = obj?.ProcessEngineerNames ?? new string[0];
                }
            }
            catch { /* Optionally log or handle error */ }
        }

        public WorkHoursController(ILogger<WorkHoursController> logger)
        {
            _logger = logger;
        }

        // POST: api/WorkHours
        [HttpPost]
        public IActionResult Post([FromBody] WorkHourDto dto)
        {
            _logger.LogInformation("Received request to save work hours for SerialNo: {SerialNo}", dto.SerialNo);
            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == dto.SerialNo);
            if (product == null)
            {
                _logger.LogWarning("Product not found for SerialNo: {SerialNo}", dto.SerialNo);
                return NotFound(new { message = "Product not found for SerialNo: " + dto.SerialNo });
            }
            var workHour = new WorkHour
            {
                WorkerName = dto.WorkerName,
                EffectiveHours = dto.MainTime,
                StartTime = DateTime.Now.AddHours(-dto.MainTime),
                EndTime = DateTime.Now,
                ProductId = product.Id
            };
            db.WorkHours.Add(workHour);
            db.SaveChanges();
            _logger.LogInformation("Work hours saved successfully for SerialNo: {SerialNo}", dto.SerialNo);
            return Ok(new { message = "Work hours saved", data = new { workHour.Id, workHour.WorkerName, workHour.EffectiveHours, workHour.StartTime, workHour.EndTime } });
        }

        // POST: api/WorkHours/submit-work-hours
        [HttpPost("submit-work-hours")]
        public IActionResult SubmitWorkHours([FromBody] SubmitWorkHoursDto dto)
        {
            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == dto.SerialNo);
            if (product == null)
            {
                return NotFound(new { message = $"Product not found for SerialNo: {dto.SerialNo}" });
            }
            var workHour = new WorkHour
            {
                WorkerName = dto.WorkerName,
                ProcessName = dto.ProcessName,
                EffectiveHours = dto.Hours,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                ProductId = product.Id
            };
            db.WorkHours.Add(workHour);
            db.SaveChanges();
            return Ok(new { message = "Work hours submitted", data = new { workHour.Id, workHour.WorkerName, workHour.ProcessName, workHour.EffectiveHours, workHour.StartTime, workHour.EndTime } });
        }

        // GET: api/WorkHours/product-status/{serialNo}
        [HttpGet("product-status/{serialNo}")]
        public IActionResult GetProductWorkStatus(string serialNo)
        {
            _logger.LogInformation("Fetching product work status for SerialNo: {SerialNo}", serialNo);
            using var db = new AppDbContext();
            var product = db.Products
                .FirstOrDefault(p => p.SerialNo == serialNo);
            if (product == null)
            {
                _logger.LogWarning("Product not found for SerialNo: {SerialNo}", serialNo);
                return NotFound(new { message = "Product not found for SerialNo: " + serialNo });
            }
            _logger.LogInformation("Product work status fetched successfully for SerialNo: {SerialNo}", serialNo);
            return Ok(product);
        }

        // GET: api/WorkHours/workhours-by-system/{serialNo}
        [HttpGet("workhours-by-system/{serialNo}")]
        public IActionResult GetWorkHoursBySystem(string serialNo)
        {
            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == serialNo);
            if (product == null) return NotFound(new { message = "Product not found" });

            // Only include Completed work hours for accumulation
            var list = db.WorkHours
                .AsNoTracking()
                .Include(w => w.Product)
                .Where(w => w.ProductId == product.Id && w.State == "Completed")
                .OrderByDescending(w => w.StartTime)
                .Select(w => new
                {
                    Id = w.Id,
                    SerialNo = product.SerialNo,
                    WorkerName = w.WorkerName,
                    ProcessName = w.ProcessName,
                    EffectiveHours = w.EffectiveHours,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    State = w.State,
                    StartTimeActual = w.StartTimeActual,
                    EndTimeActual = w.EndTimeActual
                })
                .ToList();

            return Ok(list);
        }

        // GET: api/WorkHours/ncmtimes-by-system/{serialNo}
        [HttpGet("ncmtimes-by-system/{serialNo}")]
        public IActionResult GetNcmTimesBySystem(string serialNo)
        {
            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == serialNo);
            if (product == null) return NotFound(new { message = "Product not found" });

            var list = db.NcmTimes
                .AsNoTracking()
                .Include(n => n.Product)
                .Where(n => n.ProductId == product.Id)
                .OrderByDescending(n => n.StartTime)
                .Select(n => new
                {
                    Id = n.Id,
                    SerialNo = product.SerialNo,
                    ProcessEngineer = n.ProcessEngineer,
                    ProcessName = n.ProcessName,
                    StartTime = n.StartTime,
                    EndTime = n.EndTime,
                    NcmHour = (n.EndTime - n.StartTime).TotalHours,
                    State = n.State,
                    NcmAction = n.NcmAction
                })
                .ToList();

            return Ok(list);
        }

        // GET: api/WorkHours/all-product-states
        [HttpGet("all-product-states")]
        public IActionResult GetAllProductStates()
        {
            _logger.LogInformation("Fetching all product states");
            using var db = new AppDbContext();
            var products = db.Products
                .ToList() // Fetch products into memory
                .Select(p => new {
                    SerialNo = p.SerialNo,
                    ProjectNo = p.ProjectNo,
                    SystemType = p.SystemType,
                    WorkingProcess = p.WorkingProcess,
                    WorkHourOverall = db.WorkHours
                        .Where(wh => wh.ProductId == p.Id)
                        .Sum(wh => (double?)wh.EffectiveHours) ?? 0,
                    NcmTimeOverall = db.NcmTimes
                        .Where(nt => nt.ProductId == p.Id)
                        .ToList() // Fetch NcmTimes into memory
                        .Sum(nt => (nt.EndTime - nt.StartTime).TotalHours)
                })
                .ToList();

            _logger.LogInformation("All product states fetched successfully");
            return Ok(products);
        }

        // GET: api/WorkHours/all-worker-names
        [HttpGet("all-worker-names")]
        public IActionResult GetAllWorkerNames()
        {
            return Ok(_workerNames);
        }

        // GET: api/WorkHours/all-process-names
        [HttpGet("all-process-names")]
        public IActionResult GetAllProcessNames()
        {
            return Ok(_processNames);
        }

        // GET: api/WorkHours/all-process-engineer-names
        [HttpGet("all-process-engineer-names")]
        public IActionResult GetProcessEngineerNames()
        {
            return Ok(_processEngineerNames);
        }

        // GET: api/WorkHours/worker-assignments?workerName=...
        [HttpGet("worker-assignments")]
        public IActionResult GetWorkerAssignments([FromQuery] string workerName)
        {
            if (string.IsNullOrWhiteSpace(workerName))
            {
                return BadRequest(new { message = "workerName is required" });
            }
            using var db = new AppDbContext();
            var query = db.WorkHours
                .AsNoTracking()
                .Include(w => w.Product)
                .Where(w => w.WorkerName == workerName);

            var result = query
                .OrderByDescending(w => w.StartTime)
                .Select(w => new
                {
                    SerialNo = w.Product != null ? w.Product.SerialNo : null,
                    SystemType = w.Product != null ? w.Product.SystemType : null,
                    ProcessName = w.ProcessName,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    CoWorkerName = db.WorkHours
                        .Where(o => o.ProductId == w.ProductId
                                    && o.ProcessName == w.ProcessName
                                    && o.StartTime == w.StartTime
                                    && o.EndTime == w.EndTime
                                    && o.WorkerName != w.WorkerName)
                        .Select(o => o.WorkerName)
                        .FirstOrDefault()
                })
                .ToList();

            return Ok(result);
        }

        // GET: api/WorkHours/all-workhours
        [HttpGet("all-workhours")]
        public IActionResult GetAllWorkHours()
        {
            using var db = new AppDbContext();
            var list = db.WorkHours
                .AsNoTracking()
                .Include(w => w.Product)
                .OrderByDescending(w => w.StartTime)
                .Select(w => new
                {
                    Id = w.Id,
                    SerialNo = w.Product != null ? w.Product.SerialNo : null,
                    SystemType = w.Product != null ? w.Product.SystemType : null,
                    WorkerName = w.WorkerName,
                    ProcessName = w.ProcessName,
                    EffectiveHours = w.EffectiveHours,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime
                })
                .ToList();
            return Ok(list);
        }

        // GET: api/WorkHours/all-ncmtimes
        [HttpGet("all-ncmtimes")]
        public IActionResult GetAllNcmTimes()
        {
            using var db = new AppDbContext();
            var list = db.NcmTimes
                .AsNoTracking()
                .Include(n => n.Product)
                .OrderByDescending(n => n.StartTime)
                .Select(n => new
                {
                    Id = n.Id,
                    SerialNo = n.Product != null ? n.Product.SerialNo : null,
                    SystemType = n.Product != null ? n.Product.SystemType : null,
                    ProcessEngineer = n.ProcessEngineer,
                    ProcessName = n.ProcessName,
                    StartTime = n.StartTime,
                    EndTime = n.EndTime,
                    NcmHour = (n.EndTime - n.StartTime).TotalHours
                })
                .ToList();
            return Ok(list);
        }

        // PUT: api/WorkHours/workhours/{id}
        [HttpPut("workhours/{id:int}")]
        public IActionResult UpdateWorkHour(int id, [FromBody] UpdateWorkHourDto dto)
        {
            using var db = new AppDbContext();
            var wh = db.WorkHours.FirstOrDefault(x => x.Id == id);
            if (wh == null)
            {
                return NotFound(new { message = $"WorkHour {id} not found" });
            }
            if (dto.WorkerName != null) wh.WorkerName = dto.WorkerName;
            if (dto.ProcessName != null) wh.ProcessName = dto.ProcessName;
            if (dto.EffectiveHours.HasValue)
            {
                if (dto.EffectiveHours.Value < 0) return BadRequest(new { message = "EffectiveHours cannot be negative" });
                wh.EffectiveHours = dto.EffectiveHours.Value;
            }
            if (dto.StartTime.HasValue) wh.StartTime = dto.StartTime.Value;
            if (dto.EndTime.HasValue) wh.EndTime = dto.EndTime.Value;
            if (wh.EndTime <= wh.StartTime)
            {
                return BadRequest(new { message = "EndTime must be after StartTime" });
            }
            db.SaveChanges();
            return Ok(new { message = "WorkHour updated" });
        }

        // PUT: api/WorkHours/ncmtimes/{id}
        [HttpPut("ncmtimes/{id:int}")]
        public IActionResult UpdateNcmTime(int id, [FromBody] UpdateNcmTimeDto dto)
        {
            using var db = new AppDbContext();
            var nt = db.NcmTimes.FirstOrDefault(x => x.Id == id);
            if (nt == null)
            {
                return NotFound(new { message = $"NcmTime {id} not found" });
            }
            if (dto.ProcessName != null) nt.ProcessName = dto.ProcessName;
            if (dto.ProcessEngineer != null) nt.ProcessEngineer = dto.ProcessEngineer;
            db.SaveChanges();
            return Ok(new { message = "NcmTime updated" });
        }

        // POST: api/WorkHours/set-working-by-id
        [HttpPost("set-working-by-id")]
        public IActionResult SetWorkingById([FromBody] SetWorkingByIdRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            if (string.IsNullOrWhiteSpace(req.WorkerName)) return BadRequest(new { message = "WorkerName is required" });
            if (!req.WorkHourId.HasValue) return BadRequest(new { message = "WorkHourId is required" });

            using var db = new AppDbContext();
            var target = db.WorkHours.FirstOrDefault(w => w.Id == req.WorkHourId.Value);
            if (target == null) return NotFound(new { message = "WorkHour not found" });

            target.State = "Working";
            target.WorkerName = req.WorkerName;
            if (!target.StartTimeActual.HasValue) target.StartTimeActual = DateTime.UtcNow;
            db.SaveChanges();

            return Ok(new { message = "WorkHour state of updated", id = target.Id, state = target.State, workerName = target.WorkerName });
        }

        // POST: api/WorkHours/set-working
        // Find an existing WorkHour by WorkerName, SerialNo and StartDate (date part of StartTime) and set it to Working
        [HttpPost("set-working")]
        public IActionResult SetWorking([FromBody] SetWorkingRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            if (string.IsNullOrWhiteSpace(req.WorkerName)) return BadRequest(new { message = "WorkerName is required" });
            if (string.IsNullOrWhiteSpace(req.SerialNo)) return BadRequest(new { message = "SerialNo is required" });
            if (!req.StartDate.HasValue) return BadRequest(new { message = "StartDate is required (date part of StartTime)" });

            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == req.SerialNo);
            if (product == null) return NotFound(new { message = "Product not found for SerialNo: " + req.SerialNo });

            var dateStart = req.StartDate.Value.Date;
            var dateEnd = dateStart.AddDays(1);

            var target = db.WorkHours
                .Where(w => w.ProductId == product.Id && w.WorkerName == req.WorkerName && w.StartTime >= dateStart && w.StartTime < dateEnd)
                .OrderByDescending(w => w.StartTime)
                .FirstOrDefault();

            if (target == null)
            {
                return NotFound(new { message = "No matching WorkHour record found for given worker/serial/date." });
            }

            target.State = "Working";
            target.WorkerName = req.WorkerName;
            if (!target.StartTimeActual.HasValue) target.StartTimeActual = DateTime.UtcNow;
            db.SaveChanges();

            return Ok(new { message = "WorkHour state updated", id = target.Id, state = target.State, workerName = target.WorkerName });
        }

        // POST: api/WorkHours/complete
        [HttpPost("complete")]
        public IActionResult CompleteWorkHour([FromBody] CompleteWorkHourRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            using var db = new AppDbContext();
            var wh = db.WorkHours.FirstOrDefault(w => w.Id == req.WorkHourId);
            if (wh == null) return NotFound(new { message = "WorkHour not found" });

            wh.State = "Completed";
            wh.EndTimeActual = DateTime.UtcNow;
            // optionally update WorkerName
            if (!string.IsNullOrWhiteSpace(req.WorkerName)) wh.WorkerName = req.WorkerName;
            db.SaveChanges();
            return Ok(new { message = "WorkHour completed", id = wh.Id, endTimeActual = wh.EndTimeActual });
        }

        // POST: api/WorkHours/reset
        [HttpPost("reset")]
        public IActionResult ResetWorkHour([FromBody] ResetWorkHourRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            using var db = new AppDbContext();
            var wh = db.WorkHours.FirstOrDefault(w => w.Id == req.WorkHourId);
            if (wh == null) return NotFound(new { message = "WorkHour not found" });

            wh.State = "NotStarted";
            wh.StartTimeActual = null;
            wh.EndTimeActual = null;
            db.SaveChanges();
            return Ok(new { message = "WorkHour reset", id = wh.Id });
        }

        // GET: api/WorkHours/find-workhour-id?workerName=...&serialNo=...&startDate=yyyy-MM-dd
        [HttpGet("find-workhour-id")]
        public IActionResult FindWorkHourId([FromQuery] string workerName, [FromQuery] string serialNo, [FromQuery] DateTime? startDate)
        {
            if (string.IsNullOrWhiteSpace(workerName) || string.IsNullOrWhiteSpace(serialNo) || !startDate.HasValue)
            {
                return BadRequest(new { message = "workerName, serialNo and startDate (yyyy-MM-dd) are required" });
            }

            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == serialNo);
            if (product == null) return NotFound(new { message = "Product not found for SerialNo: " + serialNo });

            var dateStart = startDate.Value.Date;
            var dateEnd = dateStart.AddDays(1);

            var wh = db.WorkHours
                .AsNoTracking()
                .Where(w => w.ProductId == product.Id && w.WorkerName == workerName && w.StartTime >= dateStart && w.StartTime < dateEnd)
                .OrderByDescending(w => w.StartTime)
                .FirstOrDefault();

            if (wh == null) return NotFound(new { message = "No WorkHour found for the given criteria" });

            return Ok(new { id = wh.Id });
        }

        private class MIProdCommInfo
        {
            public string[]? WorkerNames { get; set; }
            public string[]? ProcessNames { get; set; }
            public string[]? ProcessEngineerNames { get; set; }
        }

        public class SubmitWorkHoursDto
        {
            public string SerialNo { get; set; }
            public string WorkerName { get; set; }
            public string ProcessName { get; set; }
            public double Hours { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }

        public class UpdateWorkHourDto
        {
            public string? WorkerName { get; set; }
            public string? ProcessName { get; set; }
            public double? EffectiveHours { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
        }

        public class UpdateNcmTimeDto
        {
            public string? ProcessName { get; set; }
            public string? ProcessEngineer { get; set; }
        }

        public class IdsRequest
        {
            public int[] Ids { get; set; } = Array.Empty<int>();
        }

        public class SetWorkingRequest
        {
            public string WorkerName { get; set; } = string.Empty;
            public string SerialNo { get; set; } = string.Empty;
            public DateTime? StartDate { get; set; }
        }

        public class SetWorkingByIdRequest
        {
            public int? WorkHourId { get; set; }
            public string WorkerName { get; set; } = string.Empty;
        }

        public class CompleteWorkHourRequest
        {
            public int WorkHourId { get; set; }
            public string? WorkerName { get; set; }
        }

        public class ResetWorkHourRequest
        {
            public int WorkHourId { get; set; }
        }
    }
}
