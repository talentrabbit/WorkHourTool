using System.Globalization;
using CsvHelper;
using backend.Controllers;
using backend.DbModel;
using backend.Data;


class ImportCsvToSQLite
{
    public static void Main(string[] args)
    {
        // Usage: ImportCsvToSQLite <TableName> <CsvPath> <DbPath>
        string tableName = args.Length > 0 ? args[0].ToLower() : "products";
        string csvPath = args.Length > 1 ? args[1] : "./Products.csv";
        string dbPath = args.Length > 2 ? args[2] : "../backend/workhour.db";

        AppDbContext.ConnectionString = $"Data Source={dbPath}";
        using var db = new AppDbContext();
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

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
                    ProductId = rec.ProductId
                });
                Console.WriteLine($"Added WorkHour for ProductId={rec.ProductId}");
            }
            db.SaveChanges();
            Console.WriteLine($"Imported {records.Count} WorkHours from '{csvPath}' into database '{dbPath}'.");
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
        public double EffectiveHours { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
    }
}
