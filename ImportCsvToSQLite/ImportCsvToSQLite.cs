using System.Globalization;
using CsvHelper;
using backend.Controllers;
using backend.DbModel;
using backend.Data;
using Microsoft.EntityFrameworkCore;


class ImportCsvToSQLite
{
    public static void Main(string[] args)
    {
        // Validate and parse arguments
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: ImportCsvToSQLite <TableName> <CsvPath> <DbPath>");
            Console.WriteLine("Example: ImportCsvToSQLite workhours ./WorkHoursDummyData.csv ../backend/workhour.db");
            return;
        }

        string tableName = args[0].ToLower();
        string csvPath = args[1];
        string dbPath = args[2];

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"Error: CSV file '{csvPath}' does not exist.");
            return;
        }

        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Error: Database file '{dbPath}' does not exist.");
            return;
        }

        // Build DbContextOptions to construct AppDbContext (AppDbContext now expects DbContextOptions via DI)
        var optionsBuilder = new DbContextOptionsBuilder<backend.Data.AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        var dbOptions = optionsBuilder.Options;

        using var db = new backend.Data.AppDbContext(dbOptions);
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim(), // Trim headers to remove extra spaces
        });

        if (tableName == "products")
        {
            var records = csv.GetRecords<ProductCsv>().ToList();
            foreach (var rec in records)
            {
                var existing = db.Products.FirstOrDefault(p => p.SerialNo == rec.SerialNo);
                if (existing != null)
                {
                    existing.ProjectNo = rec.ProjectNo;
                    existing.IvkNo = rec.IvkNo;
                    existing.ModalityType = rec.ModalityType;
                    existing.ProductLine = rec.ProductLine;
                    existing.SystemType = rec.SystemType;
                    existing.UnpackageHours = rec.UnpackageHours;
                    existing.AssemblyHours = rec.AssemblyHours;
                    existing.DebugHours = rec.DebugHours;
                    existing.ValidationHours = rec.ValidationHours;
                    existing.DisassemblyHours = rec.DisassemblyHours;
                    existing.RepackageHours = rec.RepackageHours;
                    existing.WorkingProcess = rec.WorkingProcess;
                    Console.WriteLine($"Updated existing product: SerialNo={rec.SerialNo}");
                }
                else
                {
                    db.Products.Add(new Product
                    {
                        ProjectNo = rec.ProjectNo,
                        IvkNo = rec.IvkNo,
                        ModalityType = rec.ModalityType,
                        ProductLine = rec.ProductLine,
                        SystemType = rec.SystemType,
                        SerialNo = rec.SerialNo,
                        UnpackageHours = rec.UnpackageHours,
                        AssemblyHours = rec.AssemblyHours,
                        DebugHours = rec.DebugHours,
                        ValidationHours = rec.ValidationHours,
                        DisassemblyHours = rec.DisassemblyHours,
                        RepackageHours = rec.RepackageHours,
                        WorkingProcess = rec.WorkingProcess
                    });
                    Console.WriteLine($"Added new product: SerialNo={rec.SerialNo}");
                }
            }
            db.SaveChanges();
            Console.WriteLine($"Imported {records.Count} products from '{csvPath}' into database '{dbPath}'.");
        }
        else if (tableName == "ncmtimes")
        {
            var records = csv.GetRecords<NcmTimeCsv>().ToList();
            foreach (var rec in records)
            {
                db.NcmTimes.Add(new NcmTime
                {
                    ProcessEngineer = rec.ProcessEngineer,
                    StartTime = rec.StartTime,
                    EndTime = rec.EndTime,
                    ProductId = rec.ProductId
                });
                Console.WriteLine($"Added NcmTime for ProductId={rec.ProductId}");
            }
            db.SaveChanges();
            Console.WriteLine($"Imported {records.Count} NcmTimes from '{csvPath}' into database '{dbPath}'.");
        }
        else if (tableName == "workhours")
        {
            var records = csv.GetRecords<WorkHourCsv>().ToList();
            foreach (var rec in records)
            {
                db.WorkHours.Add(new WorkHour
                {
                    WorkerName = rec.WorkerName,
                    EffectiveHours = rec.EffectiveHours,
                    StartTime = rec.StartTime,
                    EndTime = rec.EndTime,
                    ProductId = rec.ProductId,
                    ProcessName = rec.ProcessName
                });
                Console.WriteLine($"Added WorkHour for ProductId={rec.ProductId}");
            }
            db.SaveChanges();
            Console.WriteLine($"Imported {records.Count} WorkHours from '{csvPath}' into database '{dbPath}'.");
        }
        else if (tableName == "users")
        {
            var records = csv.GetRecords<UserCsv>().ToList();
            foreach (var rec in records)
            {
                // normalize fields
                var gidText = (rec.Gid ?? string.Empty).Trim();
                var fullName = (rec.FullName ?? string.Empty).Trim();
                var mail = (rec.Mail ?? string.Empty).Trim();
                var role = (rec.Role ?? string.Empty).Trim();

                User? existing = null;
                if (!string.IsNullOrWhiteSpace(mail))
                {
                    existing = db.Users.FirstOrDefault(u => u.Mail == mail);
                }
                if (existing == null && !string.IsNullOrWhiteSpace(gidText))
                {
                    existing = db.Users.FirstOrDefault(u => u.Gid == gidText);
                }
                if (existing == null && !string.IsNullOrWhiteSpace(fullName))
                {
                    existing = db.Users.FirstOrDefault(u => u.FullName == fullName);
                }

                if (existing != null)
                {
                    existing.FullName = fullName;
                    existing.Mail = mail;
                    existing.Role = role;
                    Console.WriteLine($"Updated User: Mail={mail} FullName={fullName}");
                }
                else
                {
                    db.Users.Add(new User
                    {
                        FullName = fullName,
                        Mail = mail,
                        Role = role,
                        Gid = gidText
                    });
                    Console.WriteLine($"Added User: Mail={mail} FullName={fullName}");
                }
            }
            db.SaveChanges();
            Console.WriteLine($"Imported {records.Count} users from '{csvPath}' into database '{dbPath}'.");
        }
        else
        {
            Console.WriteLine($"Unsupported table: {tableName}");
        }
    }

    public class ProductCsv
    {
        public string ProjectNo { get; set; }
        public string IvkNo { get; set; }
        public string ModalityType { get; set; }
        public string ProductLine { get; set; }
        public string SystemType { get; set; }
        public string SerialNo { get; set; }
        public int UnpackageHours { get; set; }
        public int AssemblyHours { get; set; }
        public int DebugHours { get; set; }
        public int ValidationHours { get; set; }
        public int DisassemblyHours { get; set; }
        public int RepackageHours { get; set; }
        public string WorkingProcess { get; set; }
    }

    public class NcmTimeCsv
    {
        public string ProcessEngineer { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
    }

    public class WorkHourCsv
    {
        public string WorkerName { get; set; }
        public string ProcessName { get; set; }
        public double EffectiveHours { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
    }

    public class UserCsv
    {
        public string? Gid { get; set; }
        public string FullName { get; set; }
        public string Mail { get; set; }
        public string Role { get; set; }
    }
}
