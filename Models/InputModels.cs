using System.ComponentModel.DataAnnotations;

namespace dashboardWIPHouse.Models
{
    // Model for Green Hose Input Form
    public class GreenHoseInputModel
    {
        public string TransactionType { get; set; } = "IN"; // "IN" or "OUT"
        
        [Required]
        public string ItemCode { get; set; } = string.Empty;
        
        [Required]
        public string FullQR { get; set; } = string.Empty;
        
        public DateTime? ProductionDate { get; set; }
        
        public int? BoxCount { get; set; }
        
        public int? QtyEcer { get; set; } // Qty Ecer for partial boxes
        
        public int? QtyPcs { get; set; }
        
        public string? ToProcess { get; set; } // For OUTPUT - where the material goes
    }

    // Model for After Washing Input Form
    public class AfterWashingInputModel
    {
        public string TransactionType { get; set; } = "IN"; // "IN" or "SISA"
        
        [Required]
        public string ItemCode { get; set; } = string.Empty;
        
        [Required]
        public string FullQR { get; set; } = string.Empty;
        
        public DateTime? ProductionDate { get; set; }
        
        public int? BoxCount { get; set; }
        
        public int? QtyPcs { get; set; }
    }
}
