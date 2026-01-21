using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    [Table("vw_planning_aw")]
    public class PlanningFinishing
    {
        [Key]
        [Column("No_Mesin")]
        public string? NoMesin { get; set; }

        [Column("Kode_Item")]
        public string? KodeItem { get; set; }

        [Column("Qty_Plan")]
        public int? QtyPlan { get; set; }

        [Column("LOAD_TIME")]
        public DateTime? LoadTime { get; set; }

        [Column("Shift")]
        public string? Shift { get; set; }

        [Column("Keterangan")]
        public string? Keterangan { get; set; }
    }
}
