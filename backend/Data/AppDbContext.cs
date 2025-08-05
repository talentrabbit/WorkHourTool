using Microsoft.EntityFrameworkCore;
using backend.DbModel;

namespace backend.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<WorkHour> WorkHours { get; set; }
        public DbSet<NcmTime> NcmTimes { get; set; }
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
        }
    }
}
