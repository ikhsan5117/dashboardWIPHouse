using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    [Table("supply_log")]
    public class SupplyLog
    {
        [Key]
        [Column("log_id")]
        public int LogId { get; set; }

        [Column("item_code")]
        [MaxLength(100)]
        public string ItemCode { get; set; }

        [Column("full_qr")]
        [MaxLength(300)]
        public string FullQR { get; set; }

        [Column("box_count")]
        public int? BoxCount { get; set; }

        [Column("qty_pcs")]
        public int? QtyPcs { get; set; }

        [Column("supplied_at")]
        public DateTime? SuppliedAt { get; set; }

        [Column("to_process")]
        [MaxLength(100)]
        public string ToProcess { get; set; }

        [Column("tanggal")]
        [MaxLength(10)]
        public string Tanggal { get; set; }

        [Column("storage_log_id")]
        public int? StorageLogId { get; set; }
    }
}
