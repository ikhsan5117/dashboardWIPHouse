// Models/DashboardModels.cs
using System.ComponentModel.DataAnnotations;

namespace dashboardWIPHouse.Models
{
    public class DashboardItemSummary
    {
        public int TotalItems { get; set; }
        public int ExpiredCount { get; set; }
        public int NearExpiredCount { get; set; }
        public int BelowMinCount { get; set; }
        public int AboveMaxCount { get; set; }
    }

    // Request models for API
    public class UpdateItemRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public decimal QtyPerBox { get; set; } // decimal for qty_per_box
        public int StandardExp { get; set; } // int for standard_exp
        public int StandardMin { get; set; } // int for standard_min
        public int StandardMax { get; set; } // int for standard_max
    }

    public class CreateItemRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public decimal QtyPerBox { get; set; } // decimal for qty_per_box
        public int StandardExp { get; set; } // int for standard_exp
        public int StandardMin { get; set; } // int for standard_min
        public int StandardMax { get; set; } // int for standard_max
    }

    public class DeleteItemRequest
    {
        [Required]
        public string ItemCode { get; set; } = string.Empty;
    }
}