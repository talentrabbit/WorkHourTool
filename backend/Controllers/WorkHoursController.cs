
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using backend.DbModel;
using backend.Data;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WorkHoursController : ControllerBase
    {
        // POST: api/WorkHours
        [HttpPost]
        public IActionResult Post([FromBody] WorkHourDto dto)
        {
            using var db = new AppDbContext();
            var product = db.Products.FirstOrDefault(p => p.SerialNo == dto.SerialNo);
            if (product == null)
            {
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
            return Ok(new { message = "Work hours saved", data = workHour });
        }

        // GET: api/WorkHours/product-status/{serialNo}
        [HttpGet("product-status/{serialNo}")]
        public IActionResult GetProductWorkStatus(string serialNo)
        {
            using var db = new AppDbContext();
            var product = db.Products
                .Include(p => p.WorkHours)
                .Include(p => p.NcmTimes)
                .FirstOrDefault(p => p.SerialNo == serialNo);
            if (product == null)
            {
                return NotFound(new { message = "Product not found for SerialNo: " + serialNo });
            }
            return Ok(product);
        }

        // GET: api/WorkHours/all-product-states
        [HttpGet("all-product-states")]
        public IActionResult GetAllProductStates()
        {
            using var db = new AppDbContext();
            var products = db.Products.ToList();
            var result = products.Select(p => new {
                SerialNo = p.SerialNo,
                ProjectNo = p.ProjectNo,
                SystemType = p.SystemType,
                WorkingProcess = p.WorkingProcess
            }).ToList();
            return Ok(result);
        }

        // --- Example CRUD test method (not an API, for demonstration) ---
        public static void CrudTest()
        {
            using var db = new AppDbContext();
            // CREATE
            var product = new Product
            {
                ProjectNo = "P001",
                IvkNo = "IVK123",
                ModalityType = "CT",
                SystemType = "TypeA",
                SerialNo = "SN-001"
            };
            db.Products.Add(product);
            db.SaveChanges();

            // READ
            var loaded = db.Products.FirstOrDefault(p => p.SerialNo == "SN-001");

            // UPDATE
            if (loaded != null)
            {
                loaded.SystemType = "TypeB";
                db.SaveChanges();
            }

            // DELETE
            if (loaded != null)
            {
                db.Products.Remove(loaded);
                db.SaveChanges();
            }
        }
    }
}
