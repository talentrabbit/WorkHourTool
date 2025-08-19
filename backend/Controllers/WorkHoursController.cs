using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using backend.DbModel;
using backend.Data;
using System.IO;
using System.Text.Json;

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
            return Ok(new { message = "Work hours saved", data = workHour });
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
            return Ok(new { message = "Work hours submitted", data = workHour });
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
                    EndTime = w.EndTime
                })
                .ToList();

            return Ok(result);
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
    }
}
