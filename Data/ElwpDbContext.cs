using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Models;

namespace dashboardWIPHouse.Data
{
    public class ElwpDbContext : DbContext
    {
        public ElwpDbContext(DbContextOptions<ElwpDbContext> options)
            : base(options)
        {
        }

        public DbSet<PlanningElwp> PlanningElwp { get; set; }
        public DbSet<MesinElwp> MesinElwp { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            modelBuilder.Entity<PlanningElwp>(entity =>
            {
                entity.ToTable("tb_elwp_produksi_plannings", "produksi");
                entity.HasKey(e => e.Id);
            });

            modelBuilder.Entity<MesinElwp>(entity =>
            {
                entity.ToTable("tb_elwp_produksi_mesins", "produksi");
                entity.HasKey(e => e.Id);
            });
        }
    }
}
