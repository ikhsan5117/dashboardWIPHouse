using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace dashboardWIPHouse.Models
{
    [Table("Items")]
    public class Item
    {
        [Key]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Column("mesin")]
        public string? Mesin { get; set; }

        [Column("qty_per_box")]
        public decimal? QtyPerBox { get; set; } // decimal in database

        [Column("standard_exp")]
        public int? StandardExp { get; set; } // int in database (days until expiry)

        [Column("standard_min")]
        public int? StandardMin { get; set; } // int in database

        [Column("standard_max")]
        public int? StandardMax { get; set; } // int in database

        // Helper properties for display with default values
        [NotMapped]
        public decimal DisplayQtyPerBox => QtyPerBox ?? 0;

        [NotMapped]
        public int DisplayStandardExp => StandardExp ?? 0;

        [NotMapped]
        public int DisplayStandardMin => StandardMin ?? 0;

        [NotMapped]
        public int DisplayStandardMax => StandardMax ?? 0;
    }

    // Original model for Stock Summary View - represents individual records
    // NOTE: This view can have multiple records per item_code (different full_qr values)
    [Table("vw_stock_summary")]
    public class StockSummary
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

        // Changed to string to match database varchar type
        [Column("last_updated")]
        public string? LastUpdated { get; set; }

        // Navigation property to Item
        public virtual Item? Item { get; set; }

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

    // NEW: Aggregated Stock Summary Model - represents grouped data used by controller
    public class AggregatedStockSummary
    {
        public string ItemCode { get; set; } = string.Empty;
        
        // Total stock across all records for this item_code (summed from multiple records)
        public int TotalCurrentBoxStock { get; set; }
        
        // Most recent last_updated date for this item_code
        public DateTime LastUpdated { get; set; }
        
        // Associated Item data
        public Item? Item { get; set; }
        
        // Number of original records that were aggregated
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

        [NotMapped]
        public string Status
        {
            get
            {
                if (IsExpired) return "Already Expired";
                if (IsNearExpired) return "Near Expired";
                if (IsBelowMin) return "Standard Min";
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

    // Dashboard Summary Model - updated to work with aggregated data
    public class DashboardSummary
    {
        public int TotalItems { get; set; }
        public int ExpiredCount { get; set; }
        public int NearExpiredCount { get; set; }
        public int ShortageCount { get; set; } = 0;
        public int BelowMinCount { get; set; }
        public int AboveMaxCount { get; set; }

        // Computed properties for percentages
        [NotMapped]
        public double ExpiredPercentage => TotalItems > 0 ? Math.Round((double)ExpiredCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double NearExpiredPercentage => TotalItems > 0 ? Math.Round((double)NearExpiredCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double BelowMinPercentage => TotalItems > 0 ? Math.Round((double)BelowMinCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public double AboveMaxPercentage => TotalItems > 0 ? Math.Round((double)AboveMaxCount / TotalItems * 100, 1) : 0;

        [NotMapped]
        public int CriticalCount => ExpiredCount + NearExpiredCount;

        [NotMapped]
        public int StockIssueCount => BelowMinCount + AboveMaxCount;

        [NotMapped]
        public int NormalCount => TotalItems - ExpiredCount - NearExpiredCount - BelowMinCount - AboveMaxCount;

        [NotMapped]
        public double NormalPercentage => TotalItems > 0 ? Math.Round((double)NormalCount / TotalItems * 100, 1) : 0;
    }

    // Model for table display - matches what the controller's GetTableData returns
    public class StockTableItem
    {
        public int No { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public int CurrentBoxStock { get; set; }
        public decimal Pcs { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastUpdatedDate { get; set; }
        public int? StandardMin { get; set; }
        public int? StandardMax { get; set; }
        public int? StandardExp { get; set; }
        public decimal? QtyPerBox { get; set; }
        public double DaysUntilExpiry { get; set; }
    }

    // Helper class for creating aggregated data from raw stock summaries
    public static class StockSummaryAggregator
    {
        public static List<AggregatedStockSummary> AggregateStockData(List<StockSummary> rawStockSummaries)
        {
            return rawStockSummaries
                .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                .GroupBy(s => s.ItemCode)
                .Select(g => new AggregatedStockSummary
                {
                    ItemCode = g.Key,
                    TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0),
                    LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                 .OrderByDescending(s => s.ParsedLastUpdated)
                                 .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue,
                    Item = g.First().Item,
                    RecordCount = g.Count()
                })
                .ToList();
        }

        public static DashboardSummary CreateDashboardSummary(List<AggregatedStockSummary> aggregatedData)
        {
            return new DashboardSummary
            {
                TotalItems = aggregatedData.Count,
                ExpiredCount = aggregatedData.Count(x => x.IsExpired),
                NearExpiredCount = aggregatedData.Count(x => x.IsNearExpired),
                BelowMinCount = aggregatedData.Count(x => x.IsBelowMin),
                AboveMaxCount = aggregatedData.Count(x => x.IsAboveMax)
            };
        }

        public static List<StockTableItem> CreateTableData(List<AggregatedStockSummary> aggregatedData)
        {
            return aggregatedData
                .Select((item, index) => new StockTableItem
                {
                    No = index + 1,
                    ItemCode = item.ItemCode,
                    CurrentBoxStock = item.TotalCurrentBoxStock,
                    Pcs = item.TotalPcs,
                    Status = item.Status,
                    LastUpdatedDate = item.LastUpdated,
                    StandardMin = item.Item?.StandardMin,
                    StandardMax = item.Item?.StandardMax,
                    StandardExp = item.Item?.StandardExp,
                    QtyPerBox = item.Item?.QtyPerBox,
                    DaysUntilExpiry = item.DaysUntilExpiry
                })
                .OrderBy(x => x.ItemCode)
                .ToList();
        }
    }
}