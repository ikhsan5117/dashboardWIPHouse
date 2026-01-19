using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Models;

namespace dashboardWIPHouse.Data
{
    public class MoldedContext : DbContext
    {
        public MoldedContext(DbContextOptions<MoldedContext> options)
            : base(options)
        {
        }

        // MOLDED Database entities
        public DbSet<ItemMolded> ItemsMolded { get; set; }
        public DbSet<StockSummaryMolded> StockSummaryMolded { get; set; }
        public DbSet<UserMolded> UsersMolded { get; set; }
        public DbSet<ItemQCMolded> ItemsQCMolded { get; set; }
        public DbSet<ItemBCMolded> ItemsBCMolded { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure MOLDED Items table
            modelBuilder.Entity<ItemMolded>(entity =>
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

            // Configure MOLDED StockSummary view
            // NOTE: MOLDED vw_stock_summary has log_id column like Green Hose
            // Use log_id as primary key since it's unique in the view
            modelBuilder.Entity<StockSummaryMolded>(entity =>
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

                // Configure relationship with ItemsMolded table
                entity.HasOne(s => s.Item)
                    .WithMany()
                    .HasForeignKey(s => s.ItemCode)
                    .HasPrincipalKey(i => i.ItemCode)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure MOLDED Users table
            modelBuilder.Entity<UserMolded>(entity =>
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

            // Configure ItemQCMolded (Quality Check - kept for backward compatibility)
            modelBuilder.Entity<ItemQCMolded>(entity =>
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

                // Configure relationship with ItemMolded
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.KodeItem)
                      .HasPrincipalKey(i => i.ItemCode)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ItemBCMolded (Before Check - new)
            modelBuilder.Entity<ItemBCMolded>(entity =>
            {
                entity.HasKey(e => e.KodeRak);
                entity.ToTable("items_bc");

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

                // Configure relationship with ItemMolded
                entity.HasOne(e => e.Item)
                      .WithMany()
                      .HasForeignKey(e => e.KodeItem)
                      .HasPrincipalKey(i => i.ItemCode)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=10.14.149.34;Database=DB_SUPPLY_MOLDED;User Id=usrvelasto;Password=H1s@na2025!!;TrustServerCertificate=True;");
            }
        }
    }
}

