using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace dashboardWIPHouse.Models
{
    /// <summary>
    /// Model for BTR Items Master Data
    /// </summary>
    [Table("items")]
    public class ItemBTR
    {
        [Key]
        [Column("item_code")]
        [StringLength(50)]
        public string ItemCode { get; set; }

        [Column("mesin")]
        [StringLength(20)]
        public string? Mesin { get; set; }

        [Column("qty_per_box")]
        public int QtyPerBox { get; set; }

        [Column("standard_exp")]
        public int StandardExp { get; set; }

        [Column("standard_min")]
        public int StandardMin { get; set; }

        [Column("standard_max")]
        public int StandardMax { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Model for BTR Rack/Location Management
    /// </summary>
    [Table("raks")]
    public class RakBTR
    {
        [Key]
        [Column("full_qr")]
        [StringLength(100)]
        public string FullQR { get; set; }

        [Column("location")]
        [StringLength(50)]
        public string? Location { get; set; }

        [Column("item_code")]
        [StringLength(50)]
        public string ItemCode { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        [ForeignKey("ItemCode")]
        public virtual ItemBTR? Item { get; set; }
    }

    /// <summary>
    /// Model for BTR Storage Log (Incoming Materials)
    /// </summary>
    [Table("storage_log")]
    public class StorageLogBTR
    {
        [Key]
        [Column("log_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        [Column("item_code")]
        [StringLength(50)]
        public string ItemCode { get; set; }

        [Column("full_qr")]
        [StringLength(100)]
        public string? FullQR { get; set; }

        [Column("production_date")]
        public DateTime? ProductionDate { get; set; }

        [Column("box_count")]
        public int BoxCount { get; set; }

        [Column("qty_pcs")]
        public int QtyPcs { get; set; }

        [Column("stored_at")]
        public DateTime StoredAt { get; set; } = DateTime.Now;

        [Column("tanggal")]
        public DateTime Tanggal { get; set; } = DateTime.Today;

        // Navigation property
        [ForeignKey("ItemCode")]
        public virtual ItemBTR? Item { get; set; }
    }

    /// <summary>
    /// Model for BTR Supply Log (Outgoing Materials)
    /// </summary>
    [Table("supply_log")]
    public class SupplyLogBTR
    {
        [Key]
        [Column("log_id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int LogId { get; set; }

        [Column("item_code")]
        [StringLength(50)]
        public string ItemCode { get; set; }

        [Column("full_qr")]
        [StringLength(100)]
        public string? FullQR { get; set; }

        [Column("production_date")]
        public DateTime? ProductionDate { get; set; }

        [Column("box_count")]
        public int BoxCount { get; set; }

        [Column("qty_pcs")]
        public int QtyPcs { get; set; }

        [Column("supplied_at")]
        public DateTime SuppliedAt { get; set; } = DateTime.Now;

        [Column("to_process")]
        [StringLength(50)]
        public string? ToProcess { get; set; }

        [Column("tanggal")]
        public DateTime Tanggal { get; set; } = DateTime.Today;

        [Column("storage_log_id")]
        public int? StorageLogId { get; set; }

        // Navigation properties
        [ForeignKey("ItemCode")]
        public virtual ItemBTR? Item { get; set; }

        [ForeignKey("StorageLogId")]
        public virtual StorageLogBTR? StorageLog { get; set; }
    }

    /// <summary>
    /// Model for BTR Users
    /// </summary>
    [Table("users")]
    public class UserBTR
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("username")]
        [StringLength(50)]
        [Required]
        public string Username { get; set; }

        [Column("password")]
        [StringLength(255)]
        [Required]
        public string Password { get; set; }

        [Column("created_date")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
    }

    /// <summary>
    /// View Model for BTR Stock Summary
    /// </summary>
    [Table("vw_stok_summary")]
    public class StockSummaryBTR
    {
        [Key]
        [Column("log_id")]
        public int LogId { get; set; }

        [Column("item_code")]
        public string ItemCode { get; set; }

        [Column("full_qr")]
        public string? FullQR { get; set; }

        [Column("stored_at")]
        public DateTime StoredAt { get; set; }

        [Column("current_box_stock")]
        public int CurrentBoxStock { get; set; }

        [Column("standard_exp")]
        public int StandardExp { get; set; }

        [Column("expired_date")]
        public DateTime? ExpiredDate { get; set; }

        [Column("status_expired")]
        public string StatusExpired { get; set; }

        [Column("last_update")]
        public DateTime LastUpdate { get; set; }
    }

    /// <summary>
    /// Dashboard Summary ViewModel for BTR
    /// </summary>
    public class DashboardSummaryBTR
    {
        public int TotalItems { get; set; }
        public int TotalStock { get; set; }
        public int ExpiredItems { get; set; }
        public int NearExpiredItems { get; set; }
        public int BelowMinItems { get; set; }
        public int NormalItems { get; set; }
    }
}
