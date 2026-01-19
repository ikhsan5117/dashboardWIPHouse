using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    [Table("storage_log")]
    public class StorageLog
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

        [Column("production_date")]
        public DateTime? ProductionDate { get; set; }

        [Column("box_count")]
        public int? BoxCount { get; set; }

        [Column("qty_pcs")]
        public int? QtyPcs { get; set; }

        [Column("stored_at")]
        public DateTime? StoredAt { get; set; }

        [Column("tanggal")]
        [MaxLength(10)]
        public string Tanggal { get; set; }
    }
}