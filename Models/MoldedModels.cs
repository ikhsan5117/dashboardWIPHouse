using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace dashboardWIPHouse.Models
{
    // MOLDED Item Model - based on actual MOLDED database structure
    [Table("items")]
    public class ItemMolded
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

        [Column("standard_exp")]
        public int? StandardExp { get; set; }

        // Helper properties for display with default values
        [NotMapped]
        public decimal DisplayQtyPerBox => QtyPerBox ?? 0;

        [NotMapped]
        public int DisplayStandardMin => StandardMin ?? 0;

        [NotMapped]
        public int DisplayStandardMax => StandardMax ?? 0;

        [NotMapped]
        public int DisplayStandardExp => StandardExp ?? 0;
    }

    // MOLDED User Model for authentication
    [Table("users")]
    public class UserMolded
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

    // MOLDED Quality Check Model (kept for backward compatibility)
    [Table("items_qc")]
    public class ItemQCMolded
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

        // Navigation property to ItemMolded
        public virtual ItemMolded? Item { get; set; }
    }

    // MOLDED Before Check Model (new - using items_bc table)
    [Table("items_bc")]
    public class ItemBCMolded
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

        // Navigation property to ItemMolded
        public virtual ItemMolded? Item { get; set; }
    }

    // MOLDED Stock Summary View Model
    // NOTE: This view can have multiple records per item_code (different full_qr values)
    // MOLDED uses same structure as Green Hose with log_id
    [Table("vw_stock_summary")]
    public class StockSummaryMolded
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

        [Column("status_expired")]
        public string? StatusExpired { get; set; }

        // Navigation property to ItemMolded
        public virtual ItemMolded? Item { get; set; }

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

    // MOLDED Aggregated Stock Summary Model
    public class AggregatedStockSummaryMolded
    {
        public string ItemCode { get; set; } = string.Empty;
        public int TotalCurrentBoxStock { get; set; }
        public DateTime LastUpdated { get; set; }
        public ItemMolded? Item { get; set; }
        public int RecordCount { get; set; }

        // Helper properties for calculations
        [NotMapped]
        public decimal TotalPcs => Item?.QtyPerBox.HasValue == true ? 
                                  Math.Round((decimal)TotalCurrentBoxStock * Item.QtyPerBox.Value, 2) : 0;

        // Status determination methods
        [NotMapped]
        public bool IsExpired => Item?.StandardExp.HasValue == true && 
                                LastUpdated != DateTime.MinValue &&
                                CalculateDaysUntilExpiry() < 0;

        [NotMapped]
        public bool IsNearExpired => Item?.StandardExp.HasValue == true && 
                                    LastUpdated != DateTime.MinValue &&
                                    CalculateDaysUntilExpiry() >= 0 && 
                                    CalculateDaysUntilExpiry() <= GetNearExpiredThreshold();

        [NotMapped]
        public bool IsBelowMin => Item?.StandardMin.HasValue == true && 
                                 TotalCurrentBoxStock < Item.StandardMin.Value;

        [NotMapped]
        public bool IsAboveMax => Item?.StandardMax.HasValue == true && 
                                 TotalCurrentBoxStock > Item.StandardMax.Value;
                                 
        // Keep IsShortage for backward compatibility if needed, but IsBelowMin is the new standard
        [NotMapped]
        public bool IsShortage => IsBelowMin;

        [NotMapped]
        public bool IsOverStock => IsAboveMax;

        [NotMapped]
        public bool IsNormal => !IsExpired && !IsNearExpired && !IsBelowMin && !IsAboveMax;

        [NotMapped]
        public string Status
        {
            get
            {
                if (IsExpired) return "Already Expired";
                if (IsNearExpired) return "Near Expired";
                if (IsBelowMin) return "Standard Min"; // Using "Standard Min" to match Green Hose display text for shortage
                if (IsAboveMax) return "Standard Maximal";
                return "Normal";
            }
        }

        [NotMapped]
        public double DaysUntilExpiry => CalculateDaysUntilExpiry();

        // Helper method for expiry calculation
        private double CalculateDaysUntilExpiry()
        {
            if (!Item?.StandardExp.HasValue == true || LastUpdated == DateTime.MinValue) 
                return 0;
            
            var daysSinceLastUpdate = (DateTime.Now - LastUpdated).TotalDays;
            return Item.StandardExp.Value - daysSinceLastUpdate;
        }

        // Helper method to get near expired threshold based on standard exp
        private int GetNearExpiredThreshold()
        {
            if (!Item?.StandardExp.HasValue == true) return 3;
            
            // Jika standard exp <= 3 hari, maka nearly expired = 1 hari
            // Jika standard exp > 3 hari, maka nearly expired = 3 hari
            return Item.StandardExp.Value <= 3 ? 1 : 3;
        }
    }

    // MOLDED Dashboard Summary Model - matches HomeController logic
    public class DashboardSummaryMolded
    {
        public int TotalItems { get; set; }
        public int ExpiredCount { get; set; } = 0;
        public int NearExpiredCount { get; set; } = 0;
        public int ShortageCount { get; set; } = 0;
        public int BelowMinCount { get; set; } = 0;
        public int AboveMaxCount { get; set; } = 0;

        // Computed properties for percentages
        [NotMapped]
        public double ExpiredPercentage => TotalItems > 0 ? Math.Round((double)ExpiredCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double NearExpiredPercentage => TotalItems > 0 ? Math.Round((double)NearExpiredCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double ShortagePercentage => TotalItems > 0 ? Math.Round((double)ShortageCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double BelowMinPercentage => TotalItems > 0 ? Math.Round((double)BelowMinCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double AboveMaxPercentage => TotalItems > 0 ? Math.Round((double)AboveMaxCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public int CriticalCount => ExpiredCount + NearExpiredCount + ShortageCount + AboveMaxCount;

        [NotMapped]
        public double CriticalPercentage => TotalItems > 0 ? Math.Round((double)CriticalCount / TotalItems * 100, 1) : 0;
    }

    // MOLDED Stock Table Item Model
    public class StockTableItemMolded
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
        public int? StandardExp { get; set; }
        public double DaysUntilExpiry { get; set; }
    }

    // MOLDED Quality Check Dashboard Summary Model (kept for backward compatibility)
    public class QualityCheckSummaryMolded
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

    // MOLDED Quality Check Table Item Model (kept for backward compatibility)
    public class QualityCheckTableItemMolded
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

    // MOLDED Before Check Dashboard Summary Model (new)
    public class BeforeCheckSummaryMolded
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

    // MOLDED Before Check Table Item Model (new)
    public class BeforeCheckTableItemMolded
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

    // MOLDED Login View Model
    public class MoldedLoginViewModel
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

    // MOLDED Excel Upload Models
    public class ExcelRowDataMolded
    {
        public int RowNumber { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public decimal? QtyPerBox { get; set; }
        public int? StandardMin { get; set; }
        public int? StandardMax { get; set; }
        public int? StandardExp { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => !ValidationErrors.Any();
    }

    public class ExcelUploadResultMolded
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int ProcessedRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int ErrorRows { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<ExcelRowErrorMolded> DetailedErrors { get; set; } = new List<ExcelRowErrorMolded>();
    }

    public class ExcelRowErrorMolded
    {
        public int RowNumber { get; set; }
        public string Error { get; set; } = string.Empty;
        public string RowData { get; set; } = string.Empty;
    }

    // MOLDED Storage Log Model
    [Table("storage_log")]
    public class StorageLogMolded
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

        // Navigation property to ItemMolded
        public virtual ItemMolded? Item { get; set; }
    }

    // MOLDED Supply Log Model
    [Table("supply_log")]
    public class SupplyLogMolded
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



        [Column("qty_pcs")]
        public int? QtyPcs { get; set; }

        // Navigation property to ItemMolded
        public virtual ItemMolded? Item { get; set; }
    }
}
