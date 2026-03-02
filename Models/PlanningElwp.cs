using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    [Table("tb_elwp_produksi_plannings", Schema = "produksi")]
    public class PlanningElwp
    {
        [Key]
        public int Id { get; set; }
        
        public int? PlantId { get; set; }
        
        public int? AreaId { get; set; }
        
        public int? MesinId { get; set; }
        
        public DateTime? TanggalPlanning { get; set; }
        
        public string? PnSap { get; set; }
        
        public string? KodeItem { get; set; }
        
        public string? PartName { get; set; }
        
        public int? QtyPlanning { get; set; }
        
        public string? Shift { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? LoadingTimeHours { get; set; }
        
        public DateTime? CreatedAt { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        public int? CreatedBy { get; set; }
        
        [Column(TypeName = "date")]
        public DateTime? PlanningDate { get; set; }
    }
}
