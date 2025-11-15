using Microsoft.EntityFrameworkCore;
using backend.DbModel;
using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace backend.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<WorkHour> WorkHours { get; set; }
        public DbSet<NcmTime> NcmTimes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<WorkSession> WorkSessions { get; set; }

        //overwritten by appsettings.json if present
        public static string DbProvider { get; set; } = "sqlite";
        public static string ConnectionString { get; set; } = "Data Source=workhour.db";

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
            // Ensure database and tables are created
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // If the DbContext was configured via DI (AddDbContext) the optionsBuilder will already be configured.
            if (optionsBuilder.IsConfigured) return;

            // Prefer configuration in appsettings.json (deployed next to the exe). This allows changing DbPath
            // via appsettings.json when deploying the service.
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                var builder = new ConfigurationBuilder();
                if (File.Exists(configPath))
                {
                    builder.SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                    var cfg = builder.Build();
                    var configuredDbPath = cfg["DbPath"];
                    var configuredProvider = cfg["DbProvider"];

                    if (!string.IsNullOrWhiteSpace(configuredDbPath))
                    {
                        var dbPath = Path.IsPathRooted(configuredDbPath) ? configuredDbPath : Path.Combine(AppContext.BaseDirectory, configuredDbPath);
                        var conn = $"Data Source={dbPath}";
                        Console.WriteLine($"Using database path from appsettings.json: {dbPath}");
                        var provider = string.IsNullOrWhiteSpace(configuredProvider) ? DbProvider : configuredProvider;
                        if (provider?.ToLowerInvariant() == "sqlite")
                        {
                            optionsBuilder.UseSqlite(conn);
                            return;
                        }
                        else if (provider?.ToLowerInvariant() == "sqlserver")
                        {
                            optionsBuilder.UseSqlServer(conn);
                            return;
                        }
                    }
                }
            }
            catch
            {
                // ignore and fall through to static fallback
            }

            // Fallback to static configuration present for compatibility with older startup code.
            if (DbProvider == "sqlite")
            {
                optionsBuilder.UseSqlite(ConnectionString);
            }
            else if (DbProvider == "sqlserver")
            {
                optionsBuilder.UseSqlServer(ConnectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.SerialNo)
                .IsUnique();
            modelBuilder.Entity<WorkHour>()
                .HasOne(w => w.Product)
                .WithMany(p => p.WorkHours)
                .HasForeignKey(w => w.ProductId);
            modelBuilder.Entity<NcmTime>()
                .HasOne(n => n.Product)
                .WithMany(p => p.NcmTimes)
                .HasForeignKey(n => n.ProductId);

            // Ensure State column exists and has default value
            modelBuilder.Entity<WorkHour>(eb =>
            {
                eb.Property(w => w.State).HasMaxLength(50).HasDefaultValue("NotStarted");
                eb.Property(w => w.StartTimeActual).HasDefaultValueSql("CURRENT_TIMESTAMP");
                eb.Property(w => w.EndTimeActual).HasDefaultValueSql("NULL");
            });

            modelBuilder.Entity<NcmTime>(eb =>
            {
                eb.Property(n => n.State).HasMaxLength(50).HasDefaultValue("NotStarted");
                eb.Property(n => n.CallingContent).HasMaxLength(500); // renamed from NcmAction
                eb.Property(n => n.CallType).HasMaxLength(100);
                eb.Property(n => n.Actions).HasMaxLength(500);
            });

            // WorkSession snapshots for client-server session management
            modelBuilder.Entity<WorkSession>(eb =>
            {
                eb.ToTable("WorkSession");
                eb.HasKey(ws => ws.Id);
                eb.Property(ws => ws.SessionId).HasMaxLength(100).IsRequired();
                eb.HasIndex(ws => ws.SessionId).IsUnique();
                eb.Property(ws => ws.State).HasMaxLength(50).HasDefaultValue("NotStarted");
                eb.Property(ws => ws.MetadataJson).HasColumnType("TEXT");
            });

            // Users table configuration
            modelBuilder.Entity<User>(eb =>
            {
                eb.HasKey(u => u.Id);
                eb.Property(u => u.Id).ValueGeneratedOnAdd();

                // Company account id (optional, string)
                eb.Property(u => u.Gid).HasMaxLength(100);

                // Display name
                eb.Property(u => u.FullName).HasMaxLength(200).IsRequired();

                // Email and role
                eb.Property(u => u.Mail).HasMaxLength(200);
                eb.Property(u => u.Role).HasMaxLength(100);
            });
        }
    }
}
