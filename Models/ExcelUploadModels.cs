using System.ComponentModel.DataAnnotations;

namespace dashboardWIPHouse.Models
{
    public class ExcelUploadResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int ErrorRows { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<ExcelRowError> DetailedErrors { get; set; } = new List<ExcelRowError>();
    }

    public class ExcelRowError
    {
        public int RowNumber { get; set; }
        public string Error { get; set; }
        public string RowData { get; set; }
    }

    public class ExcelRowData
{
    public int RowNumber { get; set; }
    public DateTime? Timestamp { get; set; }
    public string KodeRak { get; set; }
    public string FullQR { get; set; }
    public string KodeItem { get; set; }
    public int JmlBox { get; set; }
    public DateTime? ProductionDate { get; set; }
    public int? QtyPcs { get; set; }
    public List<string> ValidationErrors { get; set; } = new List<string>();
    public bool IsValid => !ValidationErrors.Any();
}

    public class ExcelRowDataSupply
    {
        public int RowNumber { get; set; }
        public string ItemCode { get; set; }
        public string FullQR { get; set; }
        public int? BoxCount { get; set; }
        public int? QtyPcs { get; set; }
        public DateTime? SuppliedAt { get; set; }
        public string ToProcess { get; set; }
        public int? StorageLogId { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => !ValidationErrors.Any();
    }

    public class ExcelRowDataRaks
    {
        public int RowNumber { get; set; }
        public string FullQR { get; set; }
        public string Location { get; set; }
        public string ItemCode { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => !ValidationErrors.Any();
    }

    // RVI Models
    public class ExcelUploadResultRVI
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int ProcessedRows { get; set; }
        public int SuccessfulRows { get; set; }
        public int ErrorRows { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<ExcelRowErrorRVI> DetailedErrors { get; set; } = new List<ExcelRowErrorRVI>();
    }

    public class ExcelRowErrorRVI
    {
        public int RowNumber { get; set; }
        public string Error { get; set; }
        public string RowData { get; set; }
    }

    public class ExcelRowDataRVI
    {
        public int RowNumber { get; set; }
        public string ItemCode { get; set; }
        public decimal? QtyPerBox { get; set; }
        public int? StandardMin { get; set; }
        public int? StandardMax { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => !ValidationErrors.Any();
    }

    public class ExcelRowDataRVIStorage
    {
        public int RowNumber { get; set; }
        public DateTime? Timestamp { get; set; }
        public string KodeRak { get; set; }
        public string FullQR { get; set; }
        public string KodeItem { get; set; }
        public int JmlBox { get; set; }
        public DateTime? ProductionDate { get; set; }
        public int? QtyPcs { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => !ValidationErrors.Any();
    }

    public class ExcelRowDataRVISupply
    {
        public int RowNumber { get; set; }
        public DateTime? Timestamp { get; set; }
        public string KodeItem { get; set; }
        public int JmlBox { get; set; }
        public DateTime? ProductionDate { get; set; }
        public int? QtyPcs { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
        public bool IsValid => !ValidationErrors.Any();
    }
}