using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Models;

namespace dashboardWIPHouse.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<StorageLog> StorageLog { get; set; }
        public DbSet<StorageLogAW> StorageLogAW { get; set; }
        public DbSet<SupplyLog> SupplyLog { get; set; }

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<DashboardWIPHouse.Models.User> Users { get; set; }

        public DbSet<Item> Items { get; set; }
        public DbSet<StockSummary> StockSummary { get; set; }
        public DbSet<ItemAW> ItemAW { get; set; }
        public DbSet<StockSummaryAW> StockSummaryAW { get; set; }
        public DbSet<PlanningFinishing> PlanningFinishing { get; set; }
        public DbSet<Rak> Raks { get; set; }
        public DbSet<RakAW> RaksAW { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Items table
            modelBuilder.Entity<Item>(entity =>
            {
                entity.HasKey(e => e.ItemCode);
                entity.ToTable("Items");

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .IsRequired();

                entity.Property(e => e.Mesin)
                    .HasColumnName("mesin");

                // Configure columns to match database schema
                entity.Property(e => e.QtyPerBox)
                    .HasColumnName("qty_per_box")
                    .HasColumnType("decimal(10,2)")
                    .IsRequired(false);

                entity.Property(e => e.StandardExp)
                    .HasColumnName("standard_exp")
                    .HasColumnType("int")
                    .IsRequired(false);

                entity.Property(e => e.StandardMin)
                    .HasColumnName("standard_min")
                    .HasColumnType("int")
                    .IsRequired(false);

                entity.Property(e => e.StandardMax)
                    .HasColumnName("standard_max")
                    .HasColumnType("int")
                    .IsRequired(false);
            });

            // Configure Items After Washing table
            modelBuilder.Entity<ItemAW>(entity =>
            {
                // entity.HasKey(e => e.ItemCode);
                entity.ToTable("Items_aw");

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .IsRequired();

                entity.Property(e => e.Mesin)
                    .HasColumnName("mesin");

                // Configure columns to match database schema
                entity.Property(e => e.QtyPerBox)
                    .HasColumnName("qty_per_box")
                    .HasColumnType("decimal(10,2)")
                    .IsRequired(false);

                entity.Property(e => e.StandardExp)
                    .HasColumnName("standard_exp")
                    .HasColumnType("int")
                    .IsRequired(false);

                entity.Property(e => e.StandardMin)
                    .HasColumnName("standard_min")
                    .HasColumnType("int")
                    .IsRequired(false);

                entity.Property(e => e.StandardMax)
                    .HasColumnName("standard_max")
                    .HasColumnType("int")
                    .IsRequired(false);
            });

            // Configure StockSummary view - updated with string last_updated
            // NOTE: vw_stock_summary can have multiple records per item_code
            // Use log_id as primary key since it's unique in the view
            modelBuilder.Entity<StockSummary>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.ToView("vw_stock_summary");

                entity.Property(e => e.LogId)
                    .HasColumnName("log_id")
                    .IsRequired();

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .IsRequired();

                entity.Property(e => e.FullQr)
                    .HasColumnName("full_qr")
                    .IsRequired();

                entity.Property(e => e.CurrentBoxStock)
                    .HasColumnName("current_box_stock")
                    .HasColumnType("int")
                    .IsRequired(false);

                // Changed to varchar to match database type
                entity.Property(e => e.LastUpdated)
                    .HasColumnName("last_updated")
                    .HasColumnType("varchar(50)") // Adjust length as needed
                    .IsRequired(false);

                // Configure relationship with Items table
                entity.HasOne(s => s.Item)
                    .WithMany()
                    .HasForeignKey(s => s.ItemCode)
                    .HasPrincipalKey(i => i.ItemCode)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure StockSummary after washing view - updated with string last_updated
            // NOTE: vw_stock_summary_aw does NOT have log_id column
            // Use composite key of ItemCode + FullQr since there's no unique single column
            modelBuilder.Entity<StockSummaryAW>(entity =>
            {
                entity.HasKey(e => new { e.ItemCode, e.FullQr });
                entity.ToView("vw_stock_summary_aw");

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .IsRequired();

                entity.Property(e => e.FullQr)
                    .HasColumnName("full_qr")
                    .IsRequired();

                entity.Property(e => e.CurrentBoxStock)
                    .HasColumnName("current_box_stock")
                    .HasColumnType("int")
                    .IsRequired(false);

                // Changed to varchar to match database type
                entity.Property(e => e.LastUpdated)
                    .HasColumnName("last_updated")
                    .HasColumnType("varchar(50)") // Adjust length as needed
                    .IsRequired(false);

                // Configure relationship with Items table
                entity.HasOne(s => s.Item)
                    .WithMany()
                    .HasForeignKey(s => s.ItemCode)
                    .HasPrincipalKey(i => i.ItemCode)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure PlanningFinishing (View)
            modelBuilder.Entity<PlanningFinishing>(entity =>
            {
                entity.ToView("vw_planning_aw");
                // Use composite key because View doesn't have a single PK
                entity.HasKey(e => new { e.NoMesin, e.KodeItem, e.LoadTime });
                
                entity.Property(e => e.NoMesin).HasColumnName("No_Mesin");
                entity.Property(e => e.KodeItem).HasColumnName("Kode_Item");
                entity.Property(e => e.QtyPlan).HasColumnName("Qty_Plan");
                entity.Property(e => e.LoadTime).HasColumnName("LOAD_TIME");
                entity.Property(e => e.Shift).HasColumnName("Shift");
                entity.Property(e => e.Keterangan).HasColumnName("Keterangan");
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=10.14.149.34;Database=DB_SUPPLY_HOSE;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;");
            }
        }
    }
}