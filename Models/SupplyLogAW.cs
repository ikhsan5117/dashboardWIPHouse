using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    [Table("supply_log_aw")]
    public class SupplyLogAW
    {
        [Key]
        [Column("log_id")]
        public int LogId { get; set; }

        [Column("item_code")]
        public string ItemCode { get; set; }

        [Column("full_qr")]
        public string FullQr { get; set; }


        [Column("box_count")]
        public int? BoxCount { get; set; }

        [Column("qty_pcs")]
        public int? QtyPcs { get; set; }

        [Column("supplied_at")]
        public DateTime? SuppliedAt { get; set; }

        [Column("tanggal")]
        public string Tanggal { get; set; }
    }
}
