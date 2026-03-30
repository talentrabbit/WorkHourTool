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
        LogInfo("ImportCsvToSQLite started");

        // Validate and parse arguments
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: ImportCsvToSQLite <TableName> <CsvPath> <DbPath>");
            Console.WriteLine("Example: ImportCsvToSQLite workhours ./WorkHoursDummyData.csv ../backend/workhour.db");
            Console.WriteLine("Supported TableName values: products | ncmtimes | workhours | users | orders");
            return;
        }

        string tableName = args[0].ToLower();
        string csvPath = args[1];
        string dbPath = args[2];

        LogInfo($"Input -> table='{tableName}', csv='{csvPath}', db='{dbPath}'");

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

        LogInfo("Opening database context and CSV reader...");

        using var db = new backend.Data.AppDbContext(dbOptions);
        using var reader = new StreamReader(csvPath);
        using var csv = new CsvReader(reader, new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim(), // Trim headers to remove extra spaces
        });

        LogInfo("Resources initialized successfully.");

        if (tableName == "products")
        {
            LogInfo("Parsing products CSV...");
            var records = csv.GetRecords<ProductCsv>().ToList();
            LogInfo($"Parsed {records.Count} product rows. Starting upsert...");
            var updatedCount = 0;
            var addedCount = 0;

            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                Product? existing = null;
                if (rec.Id > 0)
                {
                    existing = db.Products.FirstOrDefault(p => p.Id == rec.Id);
                }
                if (existing == null)
                {
                    existing = db.Products.FirstOrDefault(p => p.SerialNo == rec.SerialNo);
                }
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
                    existing.SystemState = rec.SystemState;
                    updatedCount++;
                }
                else
                {
                    db.Products.Add(new Product
                    {
                        // Preserve CSV Id when provided; otherwise let DB generate it
                        Id = rec.Id > 0 ? rec.Id : 0,
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
                        SystemState = rec.SystemState
                    });
                    addedCount++;
                }

                if (ShouldReportProgress(i + 1, records.Count))
                {
                    LogInfo($"Products progress: {i + 1}/{records.Count} ({GetPercent(i + 1, records.Count)}%)");
                }
            }

            LogInfo("Saving product changes to database...");
            db.SaveChanges();
            LogInfo($"Products import done. Added={addedCount}, Updated={updatedCount}, Total={records.Count}");
            Console.WriteLine($"Imported {records.Count} products from '{csvPath}' into database '{dbPath}'.");
        }
        else if (tableName == "ncmtimes")
        {
            LogInfo("Parsing ncmtimes CSV...");
            var records = csv.GetRecords<NcmTimeCsv>().ToList();
            LogInfo($"Parsed {records.Count} ncmtime rows. Starting upsert...");
            var updatedCount = 0;
            var addedCount = 0;
            var skippedCount = 0;

            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (!db.Products.Any(p => p.Id == rec.ProductId))
                {
                    skippedCount++;
                    if (skippedCount <= 10)
                    {
                        LogInfo($"NcmTimes skip: ProductId={rec.ProductId} not found (row index {i + 1})");
                    }
                    continue;
                }

                NcmTime? existing = null;
                if (rec.Id > 0)
                {
                    existing = db.NcmTimes.FirstOrDefault(n => n.Id == rec.Id);
                }

                if (existing != null)
                {
                    existing.ProcessEngineer = rec.ProcessEngineer;
                    existing.ProcessName = rec.ProcessName;
                    existing.StartTime = rec.StartTime;
                    existing.EndTime = rec.EndTime;
                    existing.ProductId = rec.ProductId;
                    existing.State = string.IsNullOrWhiteSpace(rec.State) ? existing.State : rec.State;
                    existing.NcmHours = rec.NcmHours;
                    existing.CallingContent = rec.CallingContent;
                    existing.CallType = rec.CallType;
                    existing.Actions = rec.Actions;
                    updatedCount++;
                }
                else
                {
                    db.NcmTimes.Add(new NcmTime
                    {
                        // Preserve CSV Id when provided; otherwise let DB generate it
                        Id = rec.Id > 0 ? rec.Id : 0,
                        ProcessEngineer = rec.ProcessEngineer,
                        ProcessName = rec.ProcessName,
                        StartTime = rec.StartTime,
                        EndTime = rec.EndTime,
                        ProductId = rec.ProductId,
                        State = string.IsNullOrWhiteSpace(rec.State) ? "NotStarted" : rec.State,
                        NcmHours = rec.NcmHours,
                        CallingContent = rec.CallingContent,
                        CallType = rec.CallType,
                        Actions = rec.Actions
                    });
                    addedCount++;
                }

                if (ShouldReportProgress(i + 1, records.Count))
                {
                    LogInfo($"NcmTimes progress: {i + 1}/{records.Count} ({GetPercent(i + 1, records.Count)}%)");
                }
            }

            LogInfo("Saving NcmTime changes to database...");
            db.SaveChanges();
            LogInfo($"NcmTimes import done. Added={addedCount}, Updated={updatedCount}, Skipped={skippedCount}, Total={records.Count}");
            Console.WriteLine($"Imported {records.Count} NcmTimes from '{csvPath}' into database '{dbPath}'.");
        }
        else if (tableName == "workhours")
        {
            LogInfo("Parsing workhours CSV...");
            var records = csv.GetRecords<WorkHourCsv>().ToList();
            LogInfo($"Parsed {records.Count} workhour rows. Starting upsert...");
            var updatedCount = 0;
            var addedCount = 0;
            var skippedCount = 0;

            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
                if (!db.Products.Any(p => p.Id == rec.ProductId))
                {
                    skippedCount++;
                    if (skippedCount <= 10)
                    {
                        LogInfo($"WorkHours skip: ProductId={rec.ProductId} not found (row index {i + 1})");
                    }
                    continue;
                }

                WorkHour? existing = null;
                if (rec.Id > 0)
                {
                    existing = db.WorkHours.FirstOrDefault(w => w.Id == rec.Id);
                }

                if (existing != null)
                {
                    existing.WorkerName = rec.WorkerName;
                    existing.EffectiveHours = rec.EffectiveHours;
                    existing.StartTime = rec.StartTime;
                    existing.EndTime = rec.EndTime;
                    existing.ProductId = rec.ProductId;
                    existing.ProcessName = rec.ProcessName;
                    existing.State = string.IsNullOrWhiteSpace(rec.State) ? existing.State : rec.State;
                    existing.StartTimeActual = rec.StartTimeActual;
                    existing.EndTimeActual = rec.EndTimeActual;
                    existing.PlannedHours = rec.PlannedHours;
                    existing.IfToInformProductionManager = rec.IfToInformProductionManager;
                    existing.Location = rec.Location;
                    updatedCount++;
                }
                else
                {
                    db.WorkHours.Add(new WorkHour
                    {
                        // Preserve CSV Id when provided; otherwise let DB generate it
                        Id = rec.Id > 0 ? rec.Id : 0,
                        WorkerName = rec.WorkerName,
                        EffectiveHours = rec.EffectiveHours,
                        StartTime = rec.StartTime,
                        EndTime = rec.EndTime,
                        ProductId = rec.ProductId,
                        ProcessName = rec.ProcessName,
                        State = string.IsNullOrWhiteSpace(rec.State) ? "NotStarted" : rec.State,
                        StartTimeActual = rec.StartTimeActual,
                        EndTimeActual = rec.EndTimeActual,
                        PlannedHours = rec.PlannedHours,
                        IfToInformProductionManager = rec.IfToInformProductionManager,
                        Location = rec.Location
                    });
                    addedCount++;
                }

                if (ShouldReportProgress(i + 1, records.Count))
                {
                    LogInfo($"WorkHours progress: {i + 1}/{records.Count} ({GetPercent(i + 1, records.Count)}%)");
                }
            }

            LogInfo("Saving WorkHour changes to database...");
            db.SaveChanges();
            LogInfo($"WorkHours import done. Added={addedCount}, Updated={updatedCount}, Skipped={skippedCount}, Total={records.Count}");
            Console.WriteLine($"Imported {records.Count} WorkHours from '{csvPath}' into database '{dbPath}'.");
        }
        else if (tableName == "users")
        {
            LogInfo("Parsing users CSV...");
            var records = csv.GetRecords<UserCsv>().ToList();
            LogInfo($"Parsed {records.Count} user rows. Starting upsert...");
            var updatedCount = 0;
            var addedCount = 0;

            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];
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
                    updatedCount++;
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
                    addedCount++;
                }

                if (ShouldReportProgress(i + 1, records.Count))
                {
                    LogInfo($"Users progress: {i + 1}/{records.Count} ({GetPercent(i + 1, records.Count)}%)");
                }
            }

            LogInfo("Saving user changes to database...");
            db.SaveChanges();
            LogInfo($"Users import done. Added={addedCount}, Updated={updatedCount}, Total={records.Count}");
            Console.WriteLine($"Imported {records.Count} users from '{csvPath}' into database '{dbPath}'.");
        }
        else if (tableName == "orders")
        {
            LogInfo("Parsing orders CSV...");
            var records = csv.GetRecords<OrderCsv>().ToList();
            LogInfo($"Parsed {records.Count} order rows. Starting upsert...");
            var updatedCount = 0;
            var addedCount = 0;

            for (int i = 0; i < records.Count; i++)
            {
                var rec = records[i];

                var serialNo = (rec.SerialNo ?? string.Empty).Trim();
                var customer = (rec.Customer ?? string.Empty).Trim();
                var orderNumber = (rec.OrderNumber ?? string.Empty).Trim();
                var provinceCity = (rec.ProvinceCity ?? string.Empty).Trim();
                var address = (rec.Address ?? string.Empty).Trim();
                var deliveryDate = (rec.DeliveryDate ?? string.Empty).Trim();

                // Upsert priority:
                // 1) by Id (if provided)
                // 2) by SerialNo
                // 3) by OrderNumber (if not empty)
                Order? existing = null;
                if (rec.Id > 0)
                {
                    existing = db.Orders.FirstOrDefault(o => o.Id == rec.Id);
                }
                if (existing == null && !string.IsNullOrWhiteSpace(serialNo))
                {
                    existing = db.Orders.FirstOrDefault(o => o.SerialNo == serialNo);
                }
                if (existing == null && !string.IsNullOrWhiteSpace(orderNumber))
                {
                    existing = db.Orders.FirstOrDefault(o => o.OrderNumber == orderNumber);
                }

                if (existing != null)
                {
                    existing.SerialNo = serialNo;
                    existing.Customer = customer;
                    existing.OrderNumber = orderNumber;
                    existing.ProvinceCity = provinceCity;
                    existing.Address = address;
                    existing.DeliveryDate = deliveryDate;
                    updatedCount++;
                }
                else
                {
                    db.Orders.Add(new Order
                    {
                        // Preserve CSV Id when provided; otherwise let DB generate it
                        Id = rec.Id > 0 ? rec.Id : 0,
                        SerialNo = serialNo,
                        Customer = customer,
                        OrderNumber = orderNumber,
                        ProvinceCity = provinceCity,
                        Address = address,
                        DeliveryDate = deliveryDate
                    });
                    addedCount++;
                }

                if (ShouldReportProgress(i + 1, records.Count))
                {
                    LogInfo($"Orders progress: {i + 1}/{records.Count} ({GetPercent(i + 1, records.Count)}%)");
                }
            }

            LogInfo("Saving order changes to database...");
            db.SaveChanges();
            LogInfo($"Orders import done. Added={addedCount}, Updated={updatedCount}, Total={records.Count}");
            Console.WriteLine($"Imported {records.Count} orders from '{csvPath}' into database '{dbPath}'.");
        }
        else
        {
            Console.WriteLine($"Unsupported table: {tableName}");
        }

        LogInfo("ImportCsvToSQLite finished.");
    }

    private static void LogInfo(string message)
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}");
    }

    private static bool ShouldReportProgress(int current, int total)
    {
        if (total <= 0) return false;
        if (current == total) return true;
        return current % 100 == 0;
    }

    private static int GetPercent(int current, int total)
    {
        if (total <= 0) return 0;
        return (int)Math.Round((current * 100.0) / total);
    }

    public class ProductCsv
    {
        public int Id { get; set; }
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
        public string SystemState { get; set; }
        // Present in Products.csv but not persisted in Product model
        public double? SUMHOURS { get; set; }
    }

    public class NcmTimeCsv
    {
        public int Id { get; set; }
        public string ProcessEngineer { get; set; }
        public string ProcessName { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
        public string State { get; set; }
        public double? NcmHours { get; set; }
        public string? CallingContent { get; set; }
        public string? CallType { get; set; }
        public string? Actions { get; set; }
    }

    public class WorkHourCsv
    {
        public int Id { get; set; }
        public string WorkerName { get; set; }
        public string ProcessName { get; set; }
        public double EffectiveHours { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int ProductId { get; set; }
        public string State { get; set; }
        public DateTime? StartTimeActual { get; set; }
        public DateTime? EndTimeActual { get; set; }
        public double PlannedHours { get; set; }
        public bool IfToInformProductionManager { get; set; }
        public string? Location { get; set; }
    }

    public class UserCsv
    {
        public string? Gid { get; set; }
        public string FullName { get; set; }
        public string Mail { get; set; }
        public string Role { get; set; }
    }

    public class OrderCsv
    {
        public int Id { get; set; }
        public string SerialNo { get; set; }
        public string Customer { get; set; }
        public string OrderNumber { get; set; }
        public string ProvinceCity { get; set; }
        public string Address { get; set; }
        public string DeliveryDate { get; set; }
    }
}
