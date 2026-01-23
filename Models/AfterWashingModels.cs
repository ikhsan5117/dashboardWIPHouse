using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace dashboardWIPHouse.Models
{
    // Item master table for After Washing (items_aw)
    public class ItemAW
    {
        [Key]
        [Column("item_code")]
        public string ItemCode { get; set; } = string.Empty;

        [Column("mesin")]
        public string? Mesin { get; set; }

        [Column("qty_per_box")]
        public decimal? QtyPerBox { get; set; }

        [Column("standard_exp")]
        public int? StandardExp { get; set; }

        [Column("standard_min")]
        public int? StandardMin { get; set; }

        [Column("standard_max")]
        public int? StandardMax { get; set; }
    }

    // Stock summary view for After Washing (vw_stock_summary_aw)
    // NOTE: This view can have multiple records per item_code (different full_qr values)
    // IMPORTANT: This view does NOT have log_id column, so we use composite key
    public class StockSummaryAW
    {
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

        // Navigation property to ItemAW
        public ItemAW? Item { get; set; }

        // Computed property to parse the string last_updated to DateTime
        [NotMapped]
        public DateTime? ParsedLastUpdated
        {
            get
            {
                if (string.IsNullOrEmpty(LastUpdated))
                    return null;

                // Try multiple date formats
                string[] formats = {
                    "M/d/yy H:mm",
                    "M/d/yyyy H:mm", 
                    "yyyy-MM-dd HH:mm:ss",
                    "dd/MM/yyyy HH:mm:ss",
                    "dd/MM/yyyy HH:mm",
                    "MM/dd/yyyy HH:mm:ss",
                    "MM/dd/yyyy HH:mm",
                    "d/M/yyyy H:mm",
                    "d/M/yy H:mm"
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(LastUpdated, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                    {
                        return result;
                    }
                }

                // If all parsing attempts fail, return null
                return null;
            }
        }
    }

    // Request models for After Washing API operations
    public class UpdateItemAWRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public decimal QtyPerBox { get; set; }
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    public class CreateItemAWRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public decimal QtyPerBox { get; set; }
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    public class DeleteItemAWRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;
    }
}