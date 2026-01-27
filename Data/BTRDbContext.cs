using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Models;

namespace dashboardWIPHouse.Data
{
    /// <summary>
    /// Database Context for BTR (Before Trimming) Module
    /// </summary>
    public class BTRDbContext : DbContext
    {
        public BTRDbContext(DbContextOptions<BTRDbContext> options) : base(options)
        {
        }

        // Tables
        public DbSet<ItemBTR> Items { get; set; }
        public DbSet<RakBTR> Raks { get; set; }
        public DbSet<StorageLogBTR> StorageLogs { get; set; }
        public DbSet<SupplyLogBTR> SupplyLogs { get; set; }
        public DbSet<UserBTR> Users { get; set; }

        // Views
        public DbSet<StockSummaryBTR> StockSummary { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure StockSummaryBTR as keyless (it's a view)
            modelBuilder.Entity<StockSummaryBTR>()
                .HasNoKey()
                .ToView("vw_stok_summary");

            // Configure relationships
            modelBuilder.Entity<RakBTR>()
                .HasOne(r => r.Item)
                .WithMany()
                .HasForeignKey(r => r.ItemCode)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StorageLogBTR>()
                .HasOne(s => s.Item)
                .WithMany()
                .HasForeignKey(s => s.ItemCode)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplyLogBTR>()
                .HasOne(s => s.Item)
                .WithMany()
                .HasForeignKey(s => s.ItemCode)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplyLogBTR>()
                .HasOne(s => s.StorageLog)
                .WithMany()
                .HasForeignKey(s => s.StorageLogId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
