
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;

namespace backend.Controllers
{
    // --- Entity Framework Core Models ---
    public class Product
    {
        public int Id { get; set; }
        public string? ProjectNo { get; set; }
        public string? IvkNo { get; set; }
        public string? ModalityType { get; set; }
        public string? SystemType { get; set; }
        public string? SerialNo { get; set; }
        public string? ProductLine { get; set; }

        // New process hour columns
        public int UnpackageHours { get; set; }
        public int AssemblyHours { get; set; }
        public int DebugHours { get; set; }
        public int ValidationHours { get; set; }
        public int DisassemblyHours { get; set; }
        public int RepackageHours { get; set; }

        // New working process column
        public string? WorkingProcess { get; set; }

        public ICollection<WorkHour> WorkHours { get; set; } = new List<WorkHour>();
        public ICollection<NcmTime> NcmTimes { get; set; } = new List<NcmTime>();
    }

    public class WorkHour
    {
        public int Id { get; set; }
        public string? WorkerName { get; set; }
        public double EffectiveHours { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Foreign key
        public int ProductId { get; set; }
        public Product? Product { get; set; }
    }

    public class NcmTime
    {
        public int Id { get; set; }
        public string? ProcessEngineer { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Foreign key
        public int ProductId { get; set; }
        public Product? Product { get; set; }
    }

    // --- Entity Framework Core DbContext ---
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<WorkHour> WorkHours { get; set; }
        public DbSet<NcmTime> NcmTimes { get; set; }

        // Configurable database file path
        public static string DbFilePath { get; set; } = "workhour.db";

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DbFilePath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SerialNo)
                .IsUnique();
            // Optionally, configure ProductLine and WorkingProcess if needed (e.g., max length)
            // modelBuilder.Entity<Product>().Property(p => p.ProductLine).HasMaxLength(100);
            // modelBuilder.Entity<Product>().Property(p => p.WorkingProcess).HasMaxLength(100);

            modelBuilder.Entity<WorkHour>()
                .HasOne(w => w.Product)
                .WithMany(p => p.WorkHours)
                .HasForeignKey(w => w.ProductId);

            modelBuilder.Entity<NcmTime>()
                .HasOne(n => n.Product)
                .WithMany(p => p.NcmTimes)
                .HasForeignKey(n => n.ProductId);
        }
    }

    // --- Controller (unchanged) ---

    [ApiController]
    [Route("api/[controller]")]
    public class WorkHoursController : ControllerBase
    {
        // POST: api/WorkHours
        [HttpPost]
        public IActionResult Post([FromBody] WorkHourDto dto)
        {
            // Example: Save a WorkHour to the database (assumes SerialNo is provided in dto)
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

    public class WorkHourDto
    {
        public string? WorkerName { get; set; }
        public int MainTime { get; set; }
        public int IssueTime { get; set; }
        public string? SerialNo { get; set; } // Needed to associate with Product
    }
}
