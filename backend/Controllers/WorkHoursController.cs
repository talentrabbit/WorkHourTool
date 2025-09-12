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
                PlannedHours = dto.Hours,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                ProductId = product.Id
            };
            db.WorkHours.Add(workHour);
            db.SaveChanges();

            // Return a flat response to make it easy for front-end to pick up planned hours
            return Ok(new
            {
                message = "Work hours submitted",
                id = workHour.Id,
                workerName = workHour.WorkerName,
                plannedHours = workHour.PlannedHours,
                StartTime = workHour.StartTime,
                EndTime = workHour.EndTime
            });
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
                    // prefer stored NcmHours if present, otherwise compute from timestamps
                    NcmHours = n.NcmHours > 0 ? n.NcmHours : (n.EndTime - n.StartTime).TotalHours,
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
                        .Sum(nt => nt.NcmHours > 0 ? nt.NcmHours : (nt.EndTime - nt.StartTime).TotalHours)
                })
                .ToList();

            _logger.LogInformation("All product states fetched successfully");
            return Ok(products);
        }

        // GET: api/WorkHours/all-worker-names
        [HttpGet("all-worker-names")]
        public IActionResult GetAllWorkerNames()
        {
            try
            {
                using var db = new AppDbContext();
                var names = db.Users
                    .AsNoTracking()
                    .Where(u => !string.IsNullOrWhiteSpace(u.FullName) && !string.IsNullOrWhiteSpace(u.Role) && u.Role.ToLower().Contains("worker"))
                    .Select(u => u.FullName!)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToArray();

                if (names != null && names.Length > 0) return Ok(names);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to fetch worker names from Users table");
            }

            // fallback to configured list if DB lookup fails or returns nothing
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
            try
            {
                using var db = new AppDbContext();
                var names = db.Users
                    .AsNoTracking()
                    .Where(u => !string.IsNullOrWhiteSpace(u.FullName) && !string.IsNullOrWhiteSpace(u.Role) && (u.Role.ToLower().Contains("engineer") || u.Role.ToLower().Contains("process")))
                    .Select(u => u.FullName!)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToArray();

                if (names != null && names.Length > 0) return Ok(names);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to fetch process engineer names from Users table");
            }

            // fallback to configured list if DB lookup fails or returns nothing
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

            // Materialize the workhours for the worker first to avoid EF Core translation issues with complex correlated subqueries
            var rows = query
                .OrderByDescending(w => w.StartTime)
                .ToList();
            
            _logger.LogInformation("Fetching work assignments for WorkerName: {WorkerName}", workerName);

            // Build the result in-memory; for each row, perform a small lookup to find an associated co-worker if present
            var result = rows.Select(w =>
                new
                {
                    SerialNo = w.Product != null ? w.Product.SerialNo : null,
                    SystemType = w.Product != null ? w.Product.SystemType : null,
                    ProcessName = w.ProcessName,
                    State = w.State,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    CoWorkerName = db.WorkHours.AsNoTracking()
                    .Where(o => o.ProductId == w.ProductId
                                && o.ProcessName == w.ProcessName
                                && o.StartTime == w.StartTime
                                && o.EndTime == w.EndTime
                                && o.WorkerName != w.WorkerName)
                    .Select(o => o.WorkerName)
                    .FirstOrDefault()
                }
            ).ToList();
            _logger.LogInformation("Fetched {Count} assignments for WorkerName: {WorkerName}", result.Count, workerName);

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
                    PlannedHours = w.PlannedHours,
                    State = w.State,
                    StartTime = w.StartTime,
                    EndTime = w.EndTime,
                    StartTimeActual = w.StartTimeActual,
                    EndTimeActual = w.EndTimeActual
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
                    NcmHours = (n.NcmHours > 0) ? n.NcmHours : (n.EndTime - n.StartTime).TotalHours,
                    State = n.State,
                    NcmAction = n.NcmAction
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
            // support PlannedHours update
            if (dto.PlannedHours.HasValue)
            {
                if (dto.PlannedHours.Value < 0) return BadRequest(new { message = "PlannedHours cannot be negative" });
                wh.PlannedHours = dto.PlannedHours.Value;
            }
            // support state update
            if (!string.IsNullOrWhiteSpace(dto.State)) wh.State = dto.State;

            if (dto.StartTime.HasValue) wh.StartTime = dto.StartTime.Value;
            if (dto.EndTime.HasValue) wh.EndTime = dto.EndTime.Value;

            // allow saving actual timestamps when provided
            if (dto.StartTimeActual.HasValue) wh.StartTimeActual = dto.StartTimeActual.Value;
            if (dto.EndTimeActual.HasValue) wh.EndTimeActual = dto.EndTimeActual.Value;

            if (wh.EndTime <= wh.StartTime)
            {
                return BadRequest(new { message = "EndTime must be after StartTime" });
            }
            db.SaveChanges();
            return Ok(new { message = "WorkHour updated" });
        }

        // PUT: api/WorkHours/{id}
        [HttpPut("{id:int}")]
        public IActionResult UpdateWorkHourById(int id, [FromBody] UpdateWorkHourDto dto)
        {
            // delegate to existing UpdateWorkHour logic
            return UpdateWorkHour(id, dto);
        }

        // DELETE: api/WorkHours/{id}
        [HttpDelete("{id:int}")]
        public IActionResult DeleteWorkHourById(int id)
        {
            using var db = new AppDbContext();
            var wh = db.WorkHours.FirstOrDefault(x => x.Id == id);
            if (wh == null) return NotFound(new { message = $"WorkHour {id} not found" });
            db.WorkHours.Remove(wh);
            db.SaveChanges();
            return Ok(new { message = "WorkHour deleted", id });
        }

        // PUT: api/NcmTimes/{id}
        [HttpPut("~/api/NcmTimes/{id:int}")]
        public IActionResult UpdateNcmTimeByRoot(int id, [FromBody] UpdateNcmTimeDto dto)
        {
            using var db = new AppDbContext();
            var nt = db.NcmTimes.FirstOrDefault(n => n.Id == id);
            if (nt == null)
            {
                return NotFound(new { message = $"NcmTime id {id} not found" });
            }

            // update only provided fields
            if (dto.ProcessName != null) nt.ProcessName = dto.ProcessName;
            if (dto.ProcessEngineer != null) nt.ProcessEngineer = dto.ProcessEngineer;
            if (dto.NcmAction != null) nt.NcmAction = dto.NcmAction;
            if (!string.IsNullOrWhiteSpace(dto.State)) nt.State = dto.State;

            if (dto.StartTime.HasValue) nt.StartTime = dto.StartTime.Value;
            if (dto.EndTime.HasValue) nt.EndTime = dto.EndTime.Value;
            if (dto.NcmHours.HasValue)
            {
                if (dto.NcmHours.Value < 0) return BadRequest(new { message = "NcmHours cannot be negative" });
                nt.NcmHours = dto.NcmHours.Value;
            }
            else
            {
                // fall back to recompute NcmHours from timestamps if not provided
                nt.NcmHours = (dto.EndTime.Value - dto.StartTime.Value).TotalHours;
            }

            // validate timestamps
            if (nt.EndTime <= nt.StartTime)
            {
                return BadRequest(new { message = "EndTime must be later than StartTime" });
            }

            db.SaveChanges();
            return Ok(new { message = "NcmTime updated", id = nt.Id });
        }

        // DELETE: api/NcmTimes/{id}
        [HttpDelete("~/api/NcmTimes/{id:int}")]
        public IActionResult DeleteNcmTimeByRoot(int id)
        {
            using var db = new AppDbContext();
            var nt = db.NcmTimes.FirstOrDefault(x => x.Id == id);
            if (nt == null) return NotFound(new { message = $"NcmTime {id} not found" });
            db.NcmTimes.Remove(nt);
            db.SaveChanges();
            return Ok(new { message = "NcmTime deleted", id });
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
            if (!target.StartTimeActual.HasValue) target.StartTimeActual = DateTime.Now;
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
            if (!target.StartTimeActual.HasValue) target.StartTimeActual = DateTime.Now;
            db.SaveChanges();

            return Ok(new { message = "WorkHour state updated", id = target.Id, state = target.State, workerName = target.WorkerName });
        }

        // POST: api/WorkHours/reset
        [HttpPost("reset")]
        public IActionResult ResetWorkHour([FromBody] ResetWorkHourRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            using var db = new AppDbContext();
            var wh = db.WorkHours.FirstOrDefault(w => w.Id == req.WorkHourId);
            if (wh == null) return NotFound(new { message = "WorkHour not found" });

            // Only allow reset when WorkHour is in Working or NotStarted. Reject when already Completed or other terminal states.
            if (wh.State != "Working" && wh.State != "NotStarted")
            {
                return BadRequest(new { message = $"Cannot reset WorkHour in state '{wh.State}'" });
            }

            // Reset actual timestamps and state so UI can start timers again
            wh.State = "NotStarted";
            wh.StartTimeActual = null;
            wh.EndTimeActual = null;
            db.SaveChanges();

            return Ok(new { message = "WorkHour reset", id = wh.Id });
        }

        // POST: api/WorkHours/complete
        [HttpPost("complete")]
        public IActionResult CompleteWorkHour([FromBody] CompleteWorkHourRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            using var db = new AppDbContext();
            var wh = db.WorkHours.FirstOrDefault(w => w.Id == req.WorkHourId);
            if (wh == null) return NotFound(new { message = "WorkHour not found" });

            // Update EffectiveHours with reported value (from timer)
            if (req.EffectiveHours.HasValue)
            {
                wh.EffectiveHours = req.EffectiveHours.Value;
            }

            wh.State = "Completed";
            wh.EndTimeActual = DateTime.Now;
            // optionally update WorkerName
            if (!string.IsNullOrWhiteSpace(req.WorkerName)) wh.WorkerName = req.WorkerName;

            // NOTE: NCM reporting is no longer handled here. Use SaveNcmAndNotify API to create NCM records and notify engineers.

            db.SaveChanges();
            return Ok(new { message = "WorkHour completed", id = wh.Id, endTimeActual = wh.EndTimeActual });
        }
		
		// GET: api/WorkHours/email-by-user?fullname=Full Name
        [HttpGet("email-by-user")]
        public IActionResult GetEmailByUserName([FromQuery] string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return BadRequest(new { message = "fullname is required" });
            using var db = new AppDbContext();
            var user = db.Users.FirstOrDefault(u => u.FullName == fullName);
            if (user == null || string.IsNullOrWhiteSpace(user.Mail)) return NotFound(new { message = "Email not found for user" });
            return Ok(new { email = user.Mail });
        }

        // POST: api/WorkHours/save-ncm-and-notify
        [HttpPost("save-ncm-and-notify")]
        public IActionResult SaveNcmAndNotify([FromBody] NcmSaveDto[] entries)
        {
            if (entries == null || entries.Length == 0) return BadRequest(new { message = "No NCM entries provided" });
            var results = new System.Collections.Generic.List<object>();
            using var db = new AppDbContext();
            foreach (var e in entries)
            {
                try
                {
                    // resolve product by serial
                    var product = !string.IsNullOrWhiteSpace(e.SerialNo) ? db.Products.FirstOrDefault(p => p.SerialNo == e.SerialNo) : null;
                    int productId = product != null ? product.Id : 0;

                    var start = e.StartTime ?? DateTime.Now;
                    var end = e.EndTime ?? DateTime.Now;
                    if (end <= start) end = start.AddHours(1);
                    double hours = e.NcmHours > 0 ? e.NcmHours : (end - start).TotalHours;

                    var ncm = new NcmTime
                    {
                        ProcessEngineer = e.ProcessEngineer,
                        ProcessName = e.ProcessName,
                        StartTime = start,
                        EndTime = end,
                        NcmHours = hours,
                        ProductId = productId,
                        State = "Notified",
                        NcmAction = e.NcmAction
                    };
                    db.NcmTimes.Add(ncm);
                    db.SaveChanges();

                    _logger.LogInformation("NCM record saved for SerialNo: {SerialNo}, \n{ncm}", e.SerialNo, ncm);

                    // try to find recipient email and send mail via Outlook COM
                    var user = db.Users.FirstOrDefault(u => u.FullName == e.ProcessEngineer);
                    string email = user?.Mail ?? string.Empty;
                    var mailSent = false;
                    string mailError = string.Empty;
                    if (!string.IsNullOrWhiteSpace(email))
                    {
                        try
                        {
                            // late-bound COM to Outlook to avoid hard reference
                            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
                            if (outlookType != null)
                            {
                                dynamic outlook = Activator.CreateInstance(outlookType);
                                dynamic mail = outlook.CreateItem(0); // olMailItem
                                mail.To = email;
                                mail.Subject = $"NCM Notification for {e.SerialNo ?? "unknown serial"}";
                                mail.Body = $"Dear {e.ProcessEngineer},\n\nAn NCM record has been created for SerialNo: {e.SerialNo} (Process: {e.ProcessName}).\nStart: {start:yyyy-MM-dd HH:mm}, End: {end:yyyy-MM-dd HH:mm}, Hours: {hours:F2}\nAction: {e.NcmAction}\n\nPlease follow up accordingly.\n\n-- Factory System";
                                mail.Send();
                                mailSent = true;
                            }
                            else
                            {
                                mailError = "Outlook not available on server";
                            }
                        }
                        catch (Exception ex)
                        {
                            mailError = ex.Message;
                        }
                    }

                    results.Add(new { id = ncm.Id, serial = e.SerialNo, mail = email, mailSent, mailError });
                }
                catch (Exception ex)
                {
                    results.Add(new { error = ex.Message });
                }
            }
            return Ok(new { message = "NCM saved and notifications attempted", results });
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

        public class CompleteWorkHourRequest
        {
            public int WorkHourId { get; set; }
            public string? WorkerName { get; set; }
            public double? EffectiveHours { get; set; }
            // Optional NCM reporting
            public double? NcmHours { get; set; }
            public string? ProcessEngineer { get; set; }
            public string? NcmAction { get; set; }
        }

        public class UpdateWorkHourDto
        {
            public string? WorkerName { get; set; }
            public string? ProcessName { get; set; }
            public double? EffectiveHours { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            // new: allow updating planned hours, state and actual timestamps
            public double? PlannedHours { get; set; }
            public string? State { get; set; }
            public DateTime? StartTimeActual { get; set; }
            public DateTime? EndTimeActual { get; set; }
        }

        public class UpdateNcmTimeDto
        {
            public string? ProcessName { get; set; }
            public string? ProcessEngineer { get; set; }
            public string? NcmAction { get; set; }
            public string? State { get; set; }
            // allow updating the timestamps from the UI
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }

            public double? NcmHours { get; set; }
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

        public class ResetWorkHourRequest
        {
            public int WorkHourId { get; set; }
        }

        public class NcmSaveDto
        {
            public string? SerialNo { get; set; }
            public string? ProcessEngineer { get; set; }
            public string? ProcessName { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public string? NcmAction { get; set; }

            public double NcmHours { get; set; }
        }
    }
}
