using Microsoft.EntityFrameworkCore;
using backend.DbModel;

namespace backend.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<WorkHour> WorkHours { get; set; }
        public DbSet<NcmTime> NcmTimes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<WorkSession> WorkSessions { get; set; }
        public static string DbProvider { get; set; } = "sqlite";
        public static string ConnectionString { get; set; } = "Data Source=workhour.db";
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
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
                eb.Property(n => n.NcmAction).HasMaxLength(500);
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
