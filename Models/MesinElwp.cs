using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    [Table("tb_elwp_produksi_mesins", Schema = "produksi")]
    public class MesinElwp
    {
        [Key]
        public int Id { get; set; }

        public string? KodeMesin { get; set; }

        public string? NamaMesin { get; set; }

        public int? PlantId { get; set; }

        public int? AreaId { get; set; }

        public string? Keterangan { get; set; }

        public bool? IsActive { get; set; }

        public DateTime? CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
