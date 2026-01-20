using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace dashboardWIPHouse.Models
{
    // RVI Item Model - based on actual RVI database structure
    [Table("items")]
    public class ItemRVI
    {
        [Key]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Column("qty_per_box")]
        public decimal? QtyPerBox { get; set; }

        [Column("standard_min")]
        public int? StandardMin { get; set; }

        [Column("standard_max")]
        public int? StandardMax { get; set; }

        // Helper properties for display with default values
        [NotMapped]
        public decimal DisplayQtyPerBox => QtyPerBox ?? 0;

        [NotMapped]
        public int DisplayStandardMin => StandardMin ?? 0;

        [NotMapped]
        public int DisplayStandardMax => StandardMax ?? 0;

        // For compatibility with dashboard logic (RVI doesn't have expiry)
        [NotMapped]
        public int? StandardExp => null; // RVI doesn't track expiry
    }

    // RVI User Model for authentication
    [Table("users")]
    public class UserRVI
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("created_date")]
        public DateTime? CreatedDate { get; set; }

        [Column("last_login")]
        public DateTime? LastLogin { get; set; }
    }

    // RVI Quality Check Model (kept for backward compatibility)
    [Table("items_qc")]
    public class ItemQCRVI
    {
        [Key]
        [Column("kode_rak")]
        public string KodeRak { get; set; } = string.Empty;

        [Column("kode_item")]
        public string KodeItem { get; set; } = string.Empty;

        [Column("qty")]
        public decimal? Qty { get; set; }

        [Column("type_box")]
        public string? TypeBox { get; set; }

        [Column("max_capac_rak")]
        public int? MaxCapacRak { get; set; }

        // Helper properties for display with default values
        [NotMapped]
        public decimal DisplayQty => Qty ?? 0;

        [NotMapped]
        public int DisplayMaxCapacRak => MaxCapacRak ?? 0;

        [NotMapped]
        public string DisplayTypeBox => TypeBox ?? "N/A";

        // Navigation property to ItemRVI
        public virtual ItemRVI? Item { get; set; }
    }

    // RVI Before Check Model (new - using items_bc table)
    [Table("items_bc")]
    public class ItemBCRVI
    {
        [Key]
        [Column("kode_rak")]
        public string KodeRak { get; set; } = string.Empty;

        [Column("kode_item")]
        public string KodeItem { get; set; } = string.Empty;

        [Column("qty")]
        public decimal? Qty { get; set; }

        [Column("type_box")]
        public string? TypeBox { get; set; }

        [Column("max_capac_rak")]
        public int? MaxCapacRak { get; set; }

        // Helper properties for display with default values
        [NotMapped]
        public decimal DisplayQty => Qty ?? 0;

        [NotMapped]
        public int DisplayMaxCapacRak => MaxCapacRak ?? 0;

        [NotMapped]
        public string DisplayTypeBox => TypeBox ?? "N/A";

        // Navigation property to ItemRVI
        public virtual ItemRVI? Item { get; set; }
    }

    // RVI Stock Summary View Model
    // NOTE: This view can have multiple records per item_code (different full_qr values)
    // RVI uses same structure as Green Hose with log_id
    [Table("vw_stock_summary")]
    public class StockSummaryRVI
    {
        [Key]
        [Column("log_id")]
        public int LogId { get; set; }

        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Column("full_qr")]
        public string FullQr { get; set; } = string.Empty;

        [Column("current_box_stock")]
        public int? CurrentBoxStock { get; set; }

        [Column("last_updated")]
        public string? LastUpdated { get; set; }

        // Navigation property to ItemRVI
        public virtual ItemRVI? Item { get; set; }

        // Helper properties for display with default values
        [NotMapped]
        public int DisplayCurrentBoxStock => CurrentBoxStock ?? 0;

        // Helper method to parse string date to DateTime
        [NotMapped]
        public DateTime? ParsedLastUpdated
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LastUpdated))
                    return null;

                // Try multiple date formats commonly used
                string[] formats = {
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-dd HH:mm:ss.fff",
                    "dd/MM/yyyy HH:mm:ss",
                    "MM/dd/yyyy HH:mm:ss",
                    "yyyy-MM-dd",
                    "dd/MM/yyyy",
                    "MM/dd/yyyy"
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(LastUpdated, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                    {
                        return result;
                    }
                }

                // Fallback to general parsing
                if (DateTime.TryParse(LastUpdated, out DateTime generalResult))
                {
                    return generalResult;
                }

                return null;
            }
        }

        // Helper property to get safe DateTime for calculations
        [NotMapped]
        public DateTime SafeLastUpdated => ParsedLastUpdated ?? DateTime.MinValue;
    }

    // RVI Aggregated Stock Summary Model
    public class AggregatedStockSummaryRVI
    {
        public string ItemCode { get; set; } = string.Empty;
        public int TotalCurrentBoxStock { get; set; }
        public DateTime LastUpdated { get; set; }
        public ItemRVI? Item { get; set; }
        public int RecordCount { get; set; }

        // Helper properties for calculations
        [NotMapped]
        public decimal TotalPcs => Item?.QtyPerBox.HasValue == true ? 
                                  Math.Round((decimal)TotalCurrentBoxStock * Item.QtyPerBox.Value, 2) : 0;

        // RVI Status determination methods (no expiry logic)
        [NotMapped]
        public bool IsShortage => Item?.StandardMin.HasValue == true && 
                                 TotalCurrentBoxStock <= Item.StandardMin.Value;

        [NotMapped]
        public bool IsNormal => Item?.StandardMin.HasValue == true && Item?.StandardMax.HasValue == true && 
                               TotalCurrentBoxStock > Item.StandardMin.Value && TotalCurrentBoxStock < Item.StandardMax.Value;

        [NotMapped]
        public bool IsOverStock => Item?.StandardMax.HasValue == true && 
                                  TotalCurrentBoxStock >= Item.StandardMax.Value;

        [NotMapped]
        public string Status
        {
            get
            {
                if (IsShortage) return "Shortage";
                if (IsOverStock) return "Over Stock";
                if (IsNormal) return "Normal";
                return "Normal"; // Default to Normal if no standard values
            }
        }
    }

    // RVI Dashboard Summary Model
    public class DashboardSummaryRVI
    {
        public int TotalItems { get; set; }
        public int ShortageCount { get; set; } = 0;
        public int NormalCount { get; set; } = 0;
        public int OverStockCount { get; set; } = 0;

        // Computed properties for percentages
        [NotMapped]
        public double ShortagePercentage => TotalItems > 0 ? Math.Round((double)ShortageCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double NormalPercentage => TotalItems > 0 ? Math.Round((double)NormalCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double OverStockPercentage => TotalItems > 0 ? Math.Round((double)OverStockCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public int CriticalCount => ShortageCount + OverStockCount;

        [NotMapped]
        public double CriticalPercentage => TotalItems > 0 ? Math.Round((double)CriticalCount / TotalItems * 100, 1) : 0;
    }

    // RVI Stock Table Item Model
    public class StockTableItemRVI
    {
        public int No { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public int? StandardMin { get; set; }
        public int? StandardMax { get; set; }
        public int CurrentBoxStock { get; set; }
        public decimal Pcs { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdatedDate { get; set; }
        public decimal? QtyPerBox { get; set; }
    }

    // RVI Quality Check Dashboard Summary Model (kept for backward compatibility)
    public class QualityCheckSummaryRVI
    {
        public int TotalRaks { get; set; }
        public int TotalItems { get; set; }
        public int FullRaks { get; set; }
        public int EmptyRaks { get; set; }
        public int PartialRaks { get; set; }
        public decimal TotalQty { get; set; }

        // Computed properties for percentages
        [NotMapped]
        public double FullRaksPercentage => TotalRaks > 0 ? Math.Round((double)FullRaks / TotalRaks * 100, 1) : 0;

        [NotMapped]
        public double EmptyRaksPercentage => TotalRaks > 0 ? Math.Round((double)EmptyRaks / TotalRaks * 100, 1) : 0;

        [NotMapped]
        public double PartialRaksPercentage => TotalRaks > 0 ? Math.Round((double)PartialRaks / TotalRaks * 100, 1) : 0;

        [NotMapped]
        public int NormalRaks => TotalRaks - FullRaks - EmptyRaks - PartialRaks;

        [NotMapped]
        public double NormalRaksPercentage => TotalRaks > 0 ? Math.Round((double)NormalRaks / TotalRaks * 100, 1) : 0;
    }

    // RVI Quality Check Table Item Model (kept for backward compatibility)
    public class QualityCheckTableItemRVI
    {
        public int No { get; set; }
        public string KodeRak { get; set; } = string.Empty;
        public string KodeItem { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public string TypeBox { get; set; } = string.Empty;
        public int MaxCapacRak { get; set; }
        public string Status { get; set; } = string.Empty;
        public double CapacityPercentage { get; set; }
    }

    // RVI Before Check Dashboard Summary Model (new)
    public class BeforeCheckSummaryRVI
    {
        public int TotalRaks { get; set; }
        public int TotalItems { get; set; }
        public int FullRaks { get; set; }
        public int EmptyRaks { get; set; }
        public int PartialRaks { get; set; }
        public decimal TotalQty { get; set; }

        // Computed properties for percentages
        [NotMapped]
        public double FullRaksPercentage => TotalRaks > 0 ? Math.Round((double)FullRaks / TotalRaks * 100, 1) : 0;

        [NotMapped]
        public double EmptyRaksPercentage => TotalRaks > 0 ? Math.Round((double)EmptyRaks / TotalRaks * 100, 1) : 0;

        [NotMapped]
        public double PartialRaksPercentage => TotalRaks > 0 ? Math.Round((double)PartialRaks / TotalRaks * 100, 1) : 0;

        [NotMapped]
        public int NormalRaks => TotalRaks - FullRaks - EmptyRaks - PartialRaks;

        [NotMapped]
        public double NormalRaksPercentage => TotalRaks > 0 ? Math.Round((double)NormalRaks / TotalRaks * 100, 1) : 0;
    }

    // RVI Before Check Table Item Model (new)
    public class BeforeCheckTableItemRVI
    {
        public int No { get; set; }
        public string KodeRak { get; set; } = string.Empty;
        public string KodeItem { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public string TypeBox { get; set; } = string.Empty;
        public int MaxCapacRak { get; set; }
        public string Status { get; set; } = string.Empty;
        public double CapacityPercentage { get; set; }
    }

    // RVI Login View Model
    public class RVILoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    // RVI Storage Log Model
    [Table("storage_log")]
    public class StorageLogRVI
    {
        [Key]
        [Column("log_id")]
        public int LogId { get; set; }

        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Column("full_qr")]
        public string FullQR { get; set; } = string.Empty;

        [Column("stored_at")]
        public DateTime StoredAt { get; set; }

        [Column("box_count")]
        public int BoxCount { get; set; }

        [Column("tanggal")]
        public string Tanggal { get; set; } = string.Empty;

        [Column("production_date")]
        public DateTime? ProductionDate { get; set; }

        [Column("qty_pcs")]
        public int? QtyPcs { get; set; }

        // Navigation property to ItemRVI
        public virtual ItemRVI? Item { get; set; }
    }

    // RVI Supply Log Model
    [Table("supply_log")]
    public class SupplyLogRVI
    {
        [Key]
        [Column("log_id")]
        public int LogId { get; set; }

        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Column("full_qr")]
        public string FullQR { get; set; } = string.Empty;


        [Column("supplied_at")]
        public DateTime SuppliedAt { get; set; }

        [Column("box_count")]
        public int BoxCount { get; set; }

        [Column("tanggal")]
        public string Tanggal { get; set; } = string.Empty;

        [Column("production_date")]
        public DateTime? ProductionDate { get; set; }

        [Column("qty_pcs")]
        public int? QtyPcs { get; set; }

        // Navigation property to ItemRVI
        public virtual ItemRVI? Item { get; set; }
    }
}
