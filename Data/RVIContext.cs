using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Models;

namespace dashboardWIPHouse.Data
{
    public class RVIContext : DbContext
    {
        public RVIContext(DbContextOptions<RVIContext> options)
            : base(options)
        {
        }

        // RVI Database entities
        public DbSet<ItemRVI> ItemsRVI { get; set; }
        public DbSet<StockSummaryRVI> StockSummaryRVI { get; set; }
        public DbSet<UserRVI> UsersRVI { get; set; }
        public DbSet<ItemQCRVI> ItemsQCRVI { get; set; }
        public DbSet<ItemBCRVI> ItemsBCRVI { get; set; }
        public DbSet<StorageLogRVI> StorageLogRVI { get; set; }
        public DbSet<SupplyLogRVI> SupplyLogRVI { get; set; }
        public DbSet<RakRVI> Raks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure RVI Items table
            modelBuilder.Entity<ItemRVI>(entity =>
            {
                entity.HasKey(e => e.ItemCode);
                entity.ToTable("items");

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .IsRequired();

                entity.Property(e => e.QtyPerBox)
                    .HasColumnName("qty_per_box")
                    .HasColumnType("decimal(10,2)")
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

            // Configure RVI StockSummary view
            // NOTE: RVI vw_stock_summary has log_id column like Green Hose
            // Use log_id as primary key since it's unique in the view
            modelBuilder.Entity<StockSummaryRVI>(entity =>
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

                entity.Property(e => e.LastUpdated)
                    .HasColumnName("last_updated")
                    .HasColumnType("varchar(50)")
                    .IsRequired(false);

                // Configure relationship with ItemsRVI table
                entity.HasOne(s => s.Item)
                    .WithMany()
                    .HasForeignKey(s => s.ItemCode)
                    .HasPrincipalKey(i => i.ItemCode)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure RVI Users table
            modelBuilder.Entity<UserRVI>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("users");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .IsRequired();

                entity.Property(e => e.Username)
                    .HasColumnName("username")
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.Password)
                    .HasColumnName("password")
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(e => e.CreatedDate)
                    .HasColumnName("created_date")
                    .HasColumnType("datetime2(7)")
                    .IsRequired(false);

                entity.Property(e => e.LastLogin)
                    .HasColumnName("last_login")
                    .HasColumnType("datetime2(7)")
                    .IsRequired(false);
            });

            // Configure ItemQCRVI (Quality Check - kept for backward compatibility)
            modelBuilder.Entity<ItemQCRVI>(entity =>
            {
                entity.HasKey(e => e.KodeRak);
                entity.ToTable("items_qc");

                entity.Property(e => e.KodeRak)
                    .HasColumnName("kode_rak")
                    .HasMaxLength(10)
                    .IsRequired();

                entity.Property(e => e.KodeItem)
                    .HasColumnName("kode_item")
                    .HasMaxLength(10)
                    .IsRequired();

                entity.Property(e => e.Qty)
                    .HasColumnName("qty")
                    .HasColumnType("decimal(10,2)")
                    .IsRequired(false);

                entity.Property(e => e.TypeBox)
                    .HasColumnName("type_box")
                    .HasMaxLength(20)
                    .IsRequired(false);

                entity.Property(e => e.MaxCapacRak)
                    .HasColumnName("max_capac_rak")
                    .IsRequired(false);

                // Configure relationship with ItemRVI
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.KodeItem)
                      .HasPrincipalKey(i => i.ItemCode)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ItemBCRVI (Before Check - using same structure as items table)
            modelBuilder.Entity<ItemBCRVI>(entity =>
            {
                entity.HasKey(e => e.ItemCode);
                entity.ToTable("items_bc");

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.QtyPerBox)
                    .HasColumnName("qty_per_box")
                    .IsRequired(false);

                entity.Property(e => e.StandardMin)
                    .HasColumnName("standard_min")
                    .IsRequired(false);

                entity.Property(e => e.StandardMax)
                    .HasColumnName("standard_max")
                    .IsRequired(false);
            });

            // Configure StorageLogRVI
            modelBuilder.Entity<StorageLogRVI>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.ToTable("storage_log");

                entity.Property(e => e.LogId)
                    .HasColumnName("log_id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.FullQR)
                    .HasColumnName("full_qr")
                    .HasMaxLength(300)
                    .IsRequired();

                entity.Property(e => e.StoredAt)
                    .HasColumnName("stored_at")
                    .IsRequired();

                entity.Property(e => e.BoxCount)
                    .HasColumnName("box_count")
                    .IsRequired();

                entity.Property(e => e.Tanggal)
                    .HasColumnName("tanggal")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.ProductionDate)
                    .HasColumnName("production_date")
                    .IsRequired(false);

                entity.Property(e => e.QtyPcs)
                    .HasColumnName("qty_pcs")
                    .IsRequired(false);

                // Configure relationship with ItemRVI
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.ItemCode)
                      .HasPrincipalKey(i => i.ItemCode)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure SupplyLogRVI
            modelBuilder.Entity<SupplyLogRVI>(entity =>
            {
                entity.HasKey(e => e.LogId);
                entity.ToTable("supply_log");

                entity.Property(e => e.LogId)
                    .HasColumnName("log_id")
                    .ValueGeneratedOnAdd();

                entity.Property(e => e.ItemCode)
                    .HasColumnName("item_code")
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(e => e.FullQR)
                    .HasColumnName("full_qr")
                    .HasMaxLength(300)
                    .IsRequired(false);


                entity.Property(e => e.SuppliedAt)
                    .HasColumnName("supplied_at")
                    .IsRequired();

                entity.Property(e => e.BoxCount)
                    .HasColumnName("box_count")
                    .IsRequired();

                entity.Property(e => e.Tanggal)
                    .HasColumnName("tanggal")
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(e => e.ProductionDate)
                    .HasColumnName("production_date")
                    .IsRequired(false);

                entity.Property(e => e.QtyPcs)
                    .HasColumnName("qty_pcs")
                    .IsRequired(false);

                // Configure relationship with ItemRVI
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.ItemCode)
                      .HasPrincipalKey(i => i.ItemCode)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=10.14.149.34;Database=DB_SUPPLY_RVI;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;");
            }
        }
    }
}
