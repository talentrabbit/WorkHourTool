using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using backend.DbModel;
using backend.Data;
using System.IO;
using System.Text.Json;
using System.Linq; // added
using backend.Services;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkHoursController : ControllerBase
    {
        private readonly ILogger<WorkHoursController> _logger;
        private readonly AppDbContext _db;
        private static string[] _workerNames = new string[0];
        private static string[] _processNames = new string[0];
        private static string[] _processEngineerNames = new string[0];
        // Cached product definitions loaded once at startup to avoid file IO on every API call
        private static System.Collections.Generic.List<object> _productDefinitions = new System.Collections.Generic.List<object>();
         private readonly WorkSessionManager _sessionManager;

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

            // Load ProductDefinitions.csv once at startup and cache the parsed rows
            try
            {
                var csvPath = Path.Combine(AppContext.BaseDirectory ?? string.Empty, "ProductDefinitions.csv");
                if (System.IO.File.Exists(csvPath))
                {
                    var lines = System.IO.File.ReadAllLines(csvPath);
                    if (lines != null && lines.Length > 0)
                    {
                        var header = lines[0].Split(',').Select(h => h.Trim()).ToArray();
                        var list = new System.Collections.Generic.List<object>();
                        for (int i = 1; i < lines.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(lines[i])) continue;
                            var cols = lines[i].Split(',').Select(c => c.Trim()).ToArray();
                            string Get(string name)
                            {
                                var idx = Array.FindIndex(header, h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
                                if (idx >= 0 && idx < cols.Length) return cols[idx];
                                return string.Empty;
                            }

                            list.Add(new
                            {
                                IvkNo = Get("ProductIvk"),
                                ModalityType = Get("Modality"),
                                ProductLine = Get("ProductLine"),
                                SystemType = Get("SystemType"),
                                ProjectNo = Get("ProjectNo")
                            });
                        }
                        _productDefinitions = list;
                    }
                }
            }
            catch
            {
                // ignore: best-effort loading at startup
            }
        }

        public WorkHoursController(ILogger<WorkHoursController> logger, WorkSessionManager sessionManager, AppDbContext db)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _db = db;
        }

        // Resolve the scoped AppDbContext for this request. Prefer HttpContext scope; fallback to injected instance.
        private AppDbContext GetDb()
        {
            try
            {
                var scoped = HttpContext?.RequestServices?.GetService<AppDbContext>();
                return scoped ?? _db;
            }
            catch
            {
                return _db;
            }
        }

        // POST: api/WorkHours
        [HttpPost]
        public IActionResult Post([FromBody] WorkHourDto dto)
        {
            _logger.LogInformation("Received request to save work hours for SerialNo: {SerialNo}", dto.SerialNo);
            var db = GetDb();
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
            var db = GetDb();
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

            // If requested, attempt to record a notification request or log for production manager
            if (dto.IfToInformProductionManager)
            {
                try
                {
                    _logger?.LogInformation("Worker-submitted WorkHour {WorkHourId} flagged to inform production manager", workHour.Id);
                    // Optionally create a notification table row or send an async notification here.
                    // For now, we just log; expand in future to integrate with email/Teams/Outlook.
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to log production manager inform intent for WorkHour {WorkHourId}", workHour.Id);
                }
            }

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
            var db = GetDb();
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
            var db = GetDb();
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
            var db = GetDb();
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
                    CallingContent = n.CallingContent,
                    CallType = n.CallType,
                    Actions = n.Actions
                })
                .ToList();

            return Ok(list);
        }

        // GET: api/WorkHours/all-product-states
        [HttpGet("all-product-states")]
        public IActionResult GetAllProductStates()
        {
            _logger.LogInformation("Fetching all product states");
            var db = GetDb();
            var products = db.Products
                .ToList() // Fetch products into memory
                .Select(p => new {
                    SerialNo = p.SerialNo,
                    ProjectNo = p.ProjectNo,
                    SystemType = p.SystemType,
                    // Compute the production state as the latest (by StartTime DESC) WorkHour.ProcessName
                    // where the WorkHour.State is not 'Completed' (i.e. still in-progress or other non-completed states).
                    // If no working item, it should be treated as 'Finished'.
                    ProductionState = db.WorkHours
                        .Where(wh => wh.ProductId == p.Id)
                        .OrderByDescending(wh => wh.StartTime)
                        .FirstOrDefault(wh => wh.State != "Completed")
                        ?.ProcessName ?? "Finished",
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

        // POST: api/WorkHours/add-product
        [HttpPost("add-product")]
        public IActionResult AddProduct([FromBody] AddProductDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Request body required" });
            if (string.IsNullOrWhiteSpace(dto.SerialNo)) return BadRequest(new { message = "SerialNo is required" });

            var db = GetDb();
            var sn = dto.SerialNo.Trim();
            // check uniqueness
            if (db.Products.Any(p => p.SerialNo == sn))
            {
                return Conflict(new { message = $"SerialNo '{sn}' already exists" });
            }

            var p = new Product
            {
                SerialNo = sn,
                ModalityType = dto.ModalityType,
                ProductLine = dto.ProductLine,
                SystemType = dto.SystemType,
                IvkNo = dto.IvkNo,
                ProjectNo = dto.ProjectNo
            };
            db.Products.Add(p);
            db.SaveChanges();

            return Ok(new { message = "Product added", id = p.Id, serialNo = p.SerialNo });
        }

        // GET: api/WorkHours/all-worker-names
        [HttpGet("all-worker-names")]
        public IActionResult GetAllWorkerNames()
        {
            try
            {
                var db = GetDb();
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
                var db = GetDb();
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

        // GET: api/WorkHours/version
        // Returns backend version information. Preferred source is Version.json located in the app base directory
        // with shape { "version": "1.2.3" } or { "backend": "1.2.3" }. Falls back to assembly version.
        [HttpGet("version")]
        public IActionResult GetVersion()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory ?? string.Empty;
                var vPath = Path.Combine(baseDir, "Version.json");
                if (System.IO.File.Exists(vPath))
                {
                    var txt = System.IO.File.ReadAllText(vPath);
                    try
                    {
                        using var doc = JsonDocument.Parse(txt);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("backend", out var be))
                        {
                            return Ok(new { backend = be.GetString() ?? string.Empty });
                        }
                        if (root.TryGetProperty("version", out var v))
                        {
                            return Ok(new { backend = v.GetString() ?? string.Empty });
                        }
                    }
                    catch
                    {
                        // ignore parse errors and fallback to assembly
                    }
                }

                // Fallback: use assembly version
                var asm = System.Reflection.Assembly.GetEntryAssembly() ?? System.Reflection.Assembly.GetExecutingAssembly();
                var av = asm?.GetName()?.Version?.ToString() ?? string.Empty;
                return Ok(new { backend = av });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to determine backend version");
                return Ok(new { backend = string.Empty });
            }
        }
        
        // GET: api/WorkHours/worker-assignments?workerName=...
        [HttpGet("worker-assignments")]
        public IActionResult GetWorkerAssignments([FromQuery] string workerName, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {                   
            if (string.IsNullOrWhiteSpace(workerName))
            {
                return BadRequest(new { message = "workerName is required" });
            }
            var db = GetDb();
            var query = db.WorkHours
                .AsNoTracking()
                .Include(w => w.Product)
                .Where(w => w.WorkerName == workerName);

            // Apply optional date window filtering on StartTime to avoid returning the worker's entire history
            if (startDate.HasValue)
            {
                query = query.Where(w => w.StartTime >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(w => w.StartTime <= endDate.Value);
            }

            // Materialize the workhours for the worker first to avoid EF Core translation issues with complex correlated subqueries
            var rows = query
                .OrderByDescending(w => w.StartTime)
                .ToList();
            
            _logger.LogInformation("Fetching work assignments for WorkerName: {WorkerName} (startDate={StartDate}, endDate={EndDate})", workerName, startDate, endDate);

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
            var db = GetDb();
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
            var db = GetDb();
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
                    CallingContent = n.CallingContent,
                    CallType = n.CallType,
                    Actions = n.Actions
                })
                .ToList();
            return Ok(list);
        }

        // PUT: api/WorkHours/workhours/{id}
        [HttpPut("workhours/{id:int}")]
        public IActionResult UpdateWorkHour(int id, [FromBody] UpdateWorkHourDto dto)
        {
            var db = GetDb();
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
            var db = GetDb();
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
            var db = GetDb();
            var nt = db.NcmTimes.FirstOrDefault(n => n.Id == id);
            if (nt == null)
            {
                return NotFound(new { message = $"NcmTime id {id} not found" });
            }

            // update only provided fields
            if (dto.ProcessName != null) nt.ProcessName = dto.ProcessName;
            if (dto.ProcessEngineer != null) nt.ProcessEngineer = dto.ProcessEngineer;
            // Update CallingContent when provided
            if (dto.CallingContent != null) nt.CallingContent = dto.CallingContent;
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
            var db = GetDb();
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

            var db = GetDb();
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

            var db = GetDb();
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
            var db = GetDb();
            var wh = db.WorkHours.FirstOrDefault(w => w.Id == req.WorkHourId);
            if (wh == null) return NotFound(new { message = "WorkHour not found" });
            if (wh.State != "Working" && wh.State != "NotStarted")
            {
                return BadRequest(new { message = "Cannot reset WorkHour in state: " + wh.State });
            }

            // Reset the WorkHour to NotStarted and clear actual times
            wh.State = "NotStarted";
            wh.StartTimeActual = null;
            wh.EndTimeActual = null;
            db.SaveChanges();

            _logger.LogInformation("ResetWorkHour: WorkHour {WorkHourId} set to NotStarted", wh.Id);

            return Ok(new { message = "WorkHour reset" });
        }

        // POST: api/WorkHours/delete-session
        [HttpPost("delete-session")]
        public IActionResult DeleteSession([FromBody] DeleteSessionRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            try
            {
                bool removed = false;
                if (!string.IsNullOrWhiteSpace(req.SessionId))
                {
                    removed = _sessionManager?.RemoveSession(req.SessionId) ?? false;
                }
                else if (req.WorkHourId.HasValue)
                {
                    removed = _sessionManager?.RemoveByWorkHourId(req.WorkHourId.Value) ?? false;
                }
                else
                {
                    return BadRequest(new { message = "SessionId or WorkHourId is required" });
                }

                if (removed)
                {
                    _logger.LogInformation("DeleteSession: removed session (SessionId={SessionId}, WorkHourId={WorkHourId})", req.SessionId, req.WorkHourId);
                    return Ok(new { message = "Session removed", removed = true });
                }
                else
                {
                    return NotFound(new { message = "No matching session found", removed = false });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DeleteSession: failed to remove session (SessionId={SessionId}, WorkHourId={WorkHourId})", req?.SessionId, req?.WorkHourId);
                return StatusCode(500, new { message = "Failed to remove session" });
            }
        }

        // POST: api/WorkHours/complete
        [HttpPost("complete")]
        public IActionResult CompleteWorkHour([FromBody] CompleteWorkHourRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });
            var db = GetDb();
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
            var db = GetDb();
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
            var db = GetDb();
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
                                    // Use CallingContent from the request
                                    CallingContent = e.CallingContent
                    };
                    db.NcmTimes.Add(ncm);
                    db.SaveChanges();

                    _logger.LogInformation("NCM record saved for SerialNo: {SerialNo}, NcmId: {NcmId}", e.SerialNo, ncm.Id);

                    // try to find recipient email and send mail via Outlook COM
                    var user = db.Users.FirstOrDefault(u => u.FullName == e.ProcessEngineer);
                    string email = user?.Mail ?? string.Empty;
                    var mailSent = false;
                    string mailError = string.Empty;

                    if (user == null)
                    {
                        _logger.LogWarning("Process engineer user not found for name: {ProcessEngineer}. SerialNo: {SerialNo}", e.ProcessEngineer, e.SerialNo);
                    }
                    else if (string.IsNullOrWhiteSpace(email))
                    {
                        _logger.LogWarning("Process engineer {ProcessEngineer} has no email address configured. SerialNo: {SerialNo}", e.ProcessEngineer, e.SerialNo);
                    }

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
                                var mailContent = e.CallingContent;
                                mail.Body = $"Dear {e.ProcessEngineer},\n\nAn NCM record has been created for SerialNo: {e.SerialNo} (Process: {e.ProcessName}).\nStart: {start:yyyy-MM-dd HH:mm}, End: {end:yyyy-MM-dd HH:mm}, Hours: {hours:F2}\nContent: {mailContent}\n\nPlease follow up accordingly.\n\n-- Factory System";
                                mail.Send();
                                mailSent = true;
                                _logger.LogInformation("NCM notification email sent to {Email} for NcmId {NcmId} (SerialNo: {SerialNo})", email, ncm.Id, e.SerialNo);
                            }
                            else
                            {
                                mailError = "Outlook not available on server";
                                _logger.LogWarning("Outlook COM object not available on server when attempting to notify {ProcessEngineer} for SerialNo {SerialNo}", e.ProcessEngineer, e.SerialNo);
                            }
                        }
                        catch (Exception ex)
                        {
                            mailError = ex.Message + " | " + ex.InnerException?.Message;
                            _logger.LogError(ex, "Failed to send NCM notification to {Email} for NcmId {NcmId} (SerialNo: {SerialNo}). Error: {MailError}", email, ncm.Id, e.SerialNo, mailError);
                        }
                    }

                    results.Add(new { id = ncm.Id, serial = e.SerialNo, mail = email, mailSent, mailError });
                }
                catch (Exception ex)
                {
                    results.Add(new { error = ex.Message });
                }
            }
            _logger.LogInformation("Processed {Count} NCM entries for saving and notification", entries.Length);
            return Ok(new { message = "NCM saved and notifications attempted", results });
        }
		
		// GET: api/WorkHours/find-workhour-id?workerName=...&serialNo=...&startDateTime=yyyy-MM-ddTHH:mm:ss&processName=...
        [HttpGet("find-workhour-id")]
        public IActionResult FindWorkHourId([FromQuery] string workerName, [FromQuery] string serialNo, [FromQuery] DateTime? startDateTime, [FromQuery] string? processName)
        {
            // Validate required parameters
            if (string.IsNullOrWhiteSpace(workerName) || string.IsNullOrWhiteSpace(serialNo) || !startDateTime.HasValue)
            {
                return BadRequest(new { message = "workerName, serialNo and startDateTime are required" });
            }

            var db = GetDb();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == serialNo);
            if (product == null)
            {
                return NotFound(new { message = "Product not found for SerialNo: " + serialNo });
            }

            // Query for a workhour matching the provided full StartTime and worker. If processName is provided, use it to narrow the search.
            var query = db.WorkHours.AsNoTracking().Where(w => w.ProductId == product.Id && w.WorkerName == workerName && w.StartTime == startDateTime.Value);
            if (!string.IsNullOrWhiteSpace(processName))
            {
                var pn = processName.Trim();
                query = query.Where(w => (w.ProcessName ?? string.Empty).ToLower().Contains(pn.ToLower()));
            }

            var wh = query.OrderByDescending(w => w.StartTime).FirstOrDefault();
            if (wh == null)
            {
                return NotFound(new { message = "No matching WorkHour found" });
            }

            return Ok(new { id = wh.Id });
        }

        // POST: api/WorkHours/start-session
        [HttpPost("start-session")]
        public IActionResult StartSession([FromBody] StartSessionRequest req)
        {
            if (req == null) return BadRequest(new { message = "Request body required" });

            // Prefer the in-memory manager when available to provide live timers
            try
            {
                if (_sessionManager != null)
                {
                    var info = _sessionManager.StartOrResume(new WorkSessionManager.ManagerStartRequest
                    {
                        SessionId = null,
                        WorkHourId = req.WorkHourId,
                        WorkerName = req.WorkerName,
                        SerialNo = req.SerialNo,
                        ProcessName = req.ProcessName,
                        ElapsedSeconds = req.ElapsedSeconds,
                        ActiveClock = req.ActiveClock,
                        MetadataJson = req.MetadataJson,
                        State = req.State
                    });
                    return Ok(new { sessionId = info.SessionId, elapsedSeconds = info.ElapsedSeconds, state = info.State });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WorkSessionManager failed to StartOrResume, falling back to DB persistence");
            }

            // Fallback: existing DB-backed behavior
            var db = GetDb();
            // If caller supplied a WorkHourId, try to find an open session for that WorkHour and continue it
            if (req.WorkHourId.HasValue)
            {
                var existing = db.WorkSessions
                    .Where(s => s.WorkHourId == req.WorkHourId.Value)
                    .OrderByDescending(s => s.LastHeartbeat)
                    .FirstOrDefault(s => s.State != "Completed" && s.State != "Expired");

                if (existing != null)
                {
                    existing.LastHeartbeat = DateTime.Now;
                    if (req.ElapsedSeconds.HasValue && req.ElapsedSeconds.Value > existing.ElapsedSeconds)
                    {
                        existing.ElapsedSeconds = req.ElapsedSeconds.Value;
                    }
                    existing.ActiveClock = req.ActiveClock ?? existing.ActiveClock;
                    existing.MetadataJson = req.MetadataJson ?? existing.MetadataJson;
                    existing.State = string.IsNullOrWhiteSpace(req.State) ? existing.State : req.State;
                    db.SaveChanges();

                    return Ok(new { sessionId = existing.SessionId, elapsedSeconds = existing.ElapsedSeconds });
                }
            }

            var session = new WorkSession
            {
                SessionId = Guid.NewGuid().ToString(),
                WorkHourId = req.WorkHourId,
                WorkerName = req.WorkerName,
                SerialNo = req.SerialNo,
                ProcessName = req.ProcessName,
                StartTimeActual = DateTime.Now,
                LastHeartbeat = DateTime.Now,
                ElapsedSeconds = req.ElapsedSeconds ?? 0,
                ActiveClock = req.ActiveClock,
                MetadataJson = req.MetadataJson,
                State = string.IsNullOrWhiteSpace(req.State) ? "Working" : req.State
            };
            db.WorkSessions.Add(session);
            db.SaveChanges();
            return Ok(new { sessionId = session.SessionId, elapsedSeconds = session.ElapsedSeconds });
        }

        // POST: api/WorkHours/session-heartbeat
        [HttpPost("session-heartbeat")]
        public IActionResult SessionHeartbeat([FromBody] SessionHeartbeatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.SessionId)) return BadRequest(new { message = "SessionId is required" });

            try
            {
                if (_sessionManager != null)
                {
                    var info = _sessionManager.Heartbeat(new WorkSessionManager.ManagerHeartbeatRequest
                    {
                        SessionId = req.SessionId,
                        ElapsedSeconds = req.ElapsedSeconds,
                        ActiveClock = req.ActiveClock,
                        MetadataJson = req.MetadataJson,
                        State = req.State
                    });

                    if (info != null)
                    {
                        _logger.LogInformation("Heartbeat processed in WorkSessionManager for SessionId: {SessionId}, ElapsedSeconds: {ElapsedSeconds}, State: {State}", req.SessionId, info.ElapsedSeconds, info.State);
                        return Ok(new { message = "heartbeat recorded", elapsedSeconds = info.ElapsedSeconds, state = info.State });
                    }    
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WorkSessionManager failed to process heartbeat, falling back to DB persistence");
            }

            var db = GetDb();
            var session = db.WorkSessions.FirstOrDefault(s => s.SessionId == req.SessionId);
            if (session == null) return NotFound(new { message = "Session not found" });
            session.LastHeartbeat = DateTime.Now;
            session.ElapsedSeconds = req.ElapsedSeconds ?? session.ElapsedSeconds;
            session.ActiveClock = req.ActiveClock ?? session.ActiveClock;
            session.MetadataJson = req.MetadataJson ?? session.MetadataJson;
            session.State = req.State ?? session.State;
            db.SaveChanges();
            return Ok(new { message = "heartbeat recorded" });
        }

        // POST: api/WorkHours/pause-session
        [HttpPost("pause-session")]
        public IActionResult PauseSession([FromBody] PauseSessionRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.SessionId)) return BadRequest(new { message = "SessionId required" });
            try
            {
                if (_sessionManager != null && _sessionManager.PauseSession(req.SessionId))
                {
                    return Ok(new { message = "paused" });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to pause session via WorkSessionManager");
            }

            // fallback: update DB directly
            var db = GetDb();
            var session = db.WorkSessions.FirstOrDefault(s => s.SessionId == req.SessionId);
            if (session == null) return NotFound(new { message = "Session not found" });
            session.State = "Paused";
            session.LastHeartbeat = DateTime.Now;
            db.SaveChanges();
            return Ok(new { message = "paused" });
        }

        // POST: api/WorkHours/complete-session
        [HttpPost("complete-session")]
        public IActionResult CompleteSession([FromBody] CompleteSessionRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.SessionId)) return BadRequest(new { message = "SessionId is required" });

            try
            {
                if (_sessionManager != null && _sessionManager.CompleteSession(req.SessionId, req.ElapsedSeconds))
                {
                    return Ok(new { message = "session completed" });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "WorkSessionManager failed to complete session, falling back to DB persistence");
            }

            var db = GetDb();
            var session = db.WorkSessions.FirstOrDefault(s => s.SessionId == req.SessionId);
            if (session == null) return NotFound(new { message = "Session not found" });

            session.ElapsedSeconds = req.ElapsedSeconds ?? session.ElapsedSeconds;
            session.LastHeartbeat = DateTime.Now;
            session.State = "Completed";

            // Persist to WorkHour if referenced
            if (session.WorkHourId.HasValue)
            {
                var wh = db.WorkHours.FirstOrDefault(w => w.Id == session.WorkHourId.Value);
                if (wh != null)
                {
                    wh.EffectiveHours = Math.Round((session.ElapsedSeconds / 3600.0) * 100) / 100;
                    wh.EndTimeActual = DateTime.Now;
                    wh.State = "Completed";
                    if (!string.IsNullOrWhiteSpace(session.WorkerName)) wh.WorkerName = session.WorkerName;
                }
            }

            // Optionally persist NCM rows if metadata contains an array named 'ncmRows'
            // This section is commented out as NCM is handled instantly. Re-submitting might cause conflict.
            // if (!string.IsNullOrWhiteSpace(session.MetadataJson))
            // {
            //     try
            //     {
            //         using var doc = JsonDocument.Parse(session.MetadataJson);
            //         if (doc.RootElement.TryGetProperty("ncmRows", out var ncmRows) && ncmRows.ValueKind == JsonValueKind.Array)
            //         {
            //             int productId = 0;
            //             if (session.WorkHourId.HasValue)
            //             {
            //                 productId = db.WorkHours.Where(w => w.Id == session.WorkHourId.Value).Select(w => w.ProductId).FirstOrDefault();
            //             }

            //             foreach (var el in ncmRows.EnumerateArray())
            //             {
            //                 var pe = el.TryGetProperty("processEngineer", out var peEl) && peEl.ValueKind == JsonValueKind.String ? peEl.GetString() : null;
            //                 var callingContent = string.Empty;
            //                 // Read 'callingContent' from session metadata
            //                 if (el.TryGetProperty("callingContent", out var ccEl) && ccEl.ValueKind == JsonValueKind.String)
            //                 {
            //                     callingContent = ccEl.GetString();
            //                 }
            //                 double nh = 0;
            //                 if (el.TryGetProperty("ncmHours", out var nhEl) && nhEl.ValueKind == JsonValueKind.Number) nh = nhEl.GetDouble();

            //                 var ncm = new NcmTime
            //                 {
            //                     ProcessEngineer = pe,
            //                     ProcessName = session.ProcessName,
            //                     StartTime = DateTime.Now,
            //                     EndTime = DateTime.Now,
            //                     NcmHours = nh,
            //                     ProductId = productId,
            //                     State = "Notified",
            //                     CallingContent = callingContent
            //                 };
            //                 db.NcmTimes.Add(ncm);
            //             }
            //         }
            //     }
            //     catch (Exception ex)
            //     {
            //         _logger?.LogWarning(ex, "Failed to parse MetadataJson for session {SessionId}", session.SessionId);
            //     }
            // }

            db.SaveChanges();
            return Ok(new { message = "session completed" });
        }

        // GET: api/WorkHours/session-by-workhour/{workHourId}
        [HttpGet("session-by-workhour/{workHourId:int}")]
        public IActionResult GetSessionByWorkHour(int workHourId)
        {
            if (workHourId <= 0) return BadRequest(new { message = "WorkHourId is required" });
            try
            {
                if (_sessionManager == null)
                {
                    _logger?.LogError("WorkSessionManager not available, cannot fetch live session for WorkHourId {WorkHourId}", workHourId);
                    return NotFound();
                }
                var session = _sessionManager.GetByWorkHourId(workHourId);
                _logger?.LogInformation("Fetched session for WorkHourId {WorkHourId}: {Session}", workHourId, session == null ? "null" : session.SessionId);
                if (session == null)
                {
                    _logger?.LogInformation("No active session found for WorkHourId {WorkHourId}", workHourId);
                    return NotFound();
                }

                // Log important session information so callers and diagnostics can observe live state
                try
                {
                    if ((session.ElapsedSeconds > 0) || string.Equals(session.State, "Working", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(session.MetadataJson))
                    {
                        _logger?.LogInformation("Active session for WorkHourId {WorkHourId}: SessionId={SessionId}, ElapsedSeconds={ElapsedSeconds}, State={State}, ActiveClock={ActiveClock}, LastHeartbeat={LastHeartbeat}, HasMetadata={HasMetadata}",
                            workHourId,
                            session.SessionId,
                            session.ElapsedSeconds,
                            session.State,
                            session.ActiveClock,
                            session.LastHeartbeat,
                            !string.IsNullOrWhiteSpace(session.MetadataJson));
                    }
                }
                catch (Exception logEx)
                {
                    _logger?.LogDebug(logEx, "Failed to log session details for WorkHourId {WorkHourId}", workHourId);
                }


                return Ok(new
                    {
                        SessionId = session.SessionId,
                        WorkHourId = session.WorkHourId,
                        WorkerName = session.WorkerName,
                        ElapsedSeconds = session.ElapsedSeconds,
                        ActiveClock = session.ActiveClock,
                        State = session.State,
                        MetadataJson = session.MetadataJson,
                        LastHeartbeat = session.LastHeartbeat
                    });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while fetching session for WorkHourId {WorkHourId}", workHourId);
                return Problem(ex.Message);
            }
        }

        // GET: api/WorkHours/get-product-definitions
        [HttpGet("get-product-definitions")]
        public IActionResult GetProductDefinitions()
        {
            // Return the cached definitions loaded at startup
            return Ok(_productDefinitions);
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

            // New flag: when true, front-end requests informing production manager
            public bool IfToInformProductionManager { get; set; } = false;
        }

        public class CompleteWorkHourRequest
        {
            public int WorkHourId { get; set; }
            public string? WorkerName { get; set; }
            public double? EffectiveHours { get; set; }
            // Optional NCM reporting
            public double? NcmHours { get; set; }
            public string? ProcessEngineer { get; set; }
            // Preferred new name for textual NCM content
            public string? CallingContent { get; set; }
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
            // Modern NCM text property
            public string? CallingContent { get; set; }
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

        public class DeleteSessionRequest
        {
            public string? SessionId { get; set; }
            public int? WorkHourId { get; set; }
        }

        public class NcmSaveDto
        {
            public string? SerialNo { get; set; }
            public string? ProcessEngineer { get; set; }
            public string? ProcessName { get; set; }
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            // Preferred new property
            public string? CallingContent { get; set; }

            public double NcmHours { get; set; }
        }

        public class AddProductDto
        {
            public string SerialNo { get; set; }
            public string? ModalityType { get; set; }
            public string? ProductLine { get; set; }
            public string? SystemType { get; set; }
            public string? IvkNo { get; set; }
            public string? ProjectNo { get; set; }
        }

        public class StartSessionRequest
        {
            public int? WorkHourId { get; set; }
            public string? WorkerName { get; set; }
            public string? SerialNo { get; set; }
            public string? ProcessName { get; set; }
            public int? ElapsedSeconds { get; set; }
            public string? ActiveClock { get; set; }
            public string? MetadataJson { get; set; }
            public string? State { get; set; }
        }

        public class SessionHeartbeatRequest
        {
            public string? SessionId { get; set; }
            public int? ElapsedSeconds { get; set; }
            public string? ActiveClock { get; set; }
            public string? MetadataJson { get; set; }
            public string? State { get; set; }
        }

        public class CompleteSessionRequest
        {
            public string? SessionId { get; set; }
            public int? ElapsedSeconds { get; set; }
        }

        public class PauseSessionRequest
        {
            public string? SessionId { get; set; }
        }
    }
}
