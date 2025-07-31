using System.Globalization;
using CsvHelper;
using backend.Controllers;


class ImportProductsFromCsv
{
    public static void Main(string[] args)
    {
        // Allow CSV and DB path to be set via command-line args, else use defaults
        string csvPath = args.Length > 0 ? args[0] : "./Products.csv";
        string dbPath = args.Length > 1 ? args[1] : "../backend/workhour.db";

        // Set the static DbFilePath property before using AppDbContext
        AppDbContext.DbFilePath = dbPath;
        using var db = new AppDbContext();
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<ProductCsv>().ToList();
        foreach (var rec in records)
        {
            var existing = db.Products.FirstOrDefault(p => p.SerialNo == rec.SerialNo);
            if (existing != null)
            {
                // Overwrite all fields
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
}
