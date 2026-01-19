using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;
using System.Diagnostics;
using System.Globalization;

namespace dashboardWIPHouse.Controllers
{
    public class AfterWashingController : Controller
    {
        private readonly ILogger<AfterWashingController> _logger;
        private readonly ApplicationDbContext _context;

        public AfterWashingController(ILogger<AfterWashingController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("=== After Washing Dashboard Load Started (Items-based) ===");

                // Test database connection
                _logger.LogInformation("Testing database connection...");
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation($"Database connection result: {canConnect}");

                if (!canConnect)
                {
                    throw new Exception("Cannot connect to database");
                }

                // Load data from ItemsAW and StockSummaryAW
                _logger.LogInformation("Loading ItemsAW data...");
                var allItems = await _context.ItemAW.ToListAsync();
                _logger.LogInformation($"Loaded {allItems.Count} items from items_aw table");

                _logger.LogInformation("Loading Stock Summary AW data...");
                var allStockSummaries = await _context.StockSummaryAW.ToListAsync();
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from vw_stock_summary_aw");

                // Group stock summaries by item_code for aggregation
                // CORRECTED: Sum ALL records with same item_code (no deduplication)
                // Business rule: All records with same item_code should be summed regardless of full_qr
                var stockSummaryGrouped = allStockSummaries
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0), // Sum ALL records
                        LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue,
                        RecordCount = g.Count(),
                        Records = g.ToList(), // Keep all records for debugging
                        UniqueFullQRCount = g.Select(r => r.FullQr).Distinct().Count()
                    });

                _logger.LogInformation($"Aggregated stock data for {stockSummaryGrouped.Count} unique item codes");

                // Combine Items with Stock Summary data - SAMA SEPERTI HomeController
                var itemsWithStockData = allItems.Select(item => new
                {
                    Item = item,
                    StockData = stockSummaryGrouped.ContainsKey(item.ItemCode)
                               ? stockSummaryGrouped[item.ItemCode]
                               : null,
                    // Jika tidak ada stock data, anggap stock = 0
                    TotalCurrentBoxStock = stockSummaryGrouped.ContainsKey(item.ItemCode)
                                          ? stockSummaryGrouped[item.ItemCode].TotalCurrentBoxStock
                                          : 0,
                    LastUpdated = stockSummaryGrouped.ContainsKey(item.ItemCode)
                                 ? stockSummaryGrouped[item.ItemCode].LastUpdated
                                 : DateTime.MinValue,
                    HasStockData = stockSummaryGrouped.ContainsKey(item.ItemCode)
                }).ToList();

                _logger.LogInformation($"Combined data for {itemsWithStockData.Count} items");
                _logger.LogInformation($"Items with stock data: {itemsWithStockData.Count(x => x.HasStockData)}");
                _logger.LogInformation($"Items without stock data: {itemsWithStockData.Count(x => !x.HasStockData)}");

                var filteredItems = itemsWithStockData.ToList();

                // Calculate dashboard summary - SAMA SEPERTI HomeController
                var dashboardSummary = new DashboardSummary
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => x.TotalCurrentBoxStock > 0 && IsExpired(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => x.TotalCurrentBoxStock > 0 && IsNearExpired(x.LastUpdated, x.Item.StandardExp)),
                    ShortageCount = filteredItems.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                // Detailed logging for verification
                _logger.LogInformation($"After Washing Dashboard Summary - Items Based:");
                _logger.LogInformation($"- Total Items: {dashboardSummary.TotalItems}");
                _logger.LogInformation($"- Expired: {dashboardSummary.ExpiredCount}");
                _logger.LogInformation($"- Near Expired: {dashboardSummary.NearExpiredCount}");
                _logger.LogInformation($"- Shortage: {dashboardSummary.ShortageCount}");
                _logger.LogInformation($"- Below Min: {dashboardSummary.BelowMinCount}");
                _logger.LogInformation($"- Above Max: {dashboardSummary.AboveMaxCount}");

                // Log some sample items for debugging
                var sampleItems = filteredItems.Take(5).ToList();
                foreach (var item in sampleItems)
                {
                    _logger.LogInformation($"Sample - {item.Item.ItemCode}: Stock={item.TotalCurrentBoxStock}, " +
                        $"Min={item.Item.StandardMin}, Max={item.Item.StandardMax}, Status={DetermineItemStatus(item.TotalCurrentBoxStock, item.LastUpdated, item.Item)}");
                }

                ViewData["Title"] = "After Washing Dashboard";
                _logger.LogInformation("=== After Washing Dashboard Load Completed Successfully (Items-based) ===");
                return View(dashboardSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError($"=== After Washing Dashboard Load Failed ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                var emptySummary = new DashboardSummary();
                ViewData["Title"] = "After Washing Dashboard";
                ViewData["Error"] = $"Unable to load dashboard data: {ex.Message}";
                return View(emptySummary);
            }
        }

        [HttpPost]
        public async Task<JsonResult> UploadExcel(IFormFile file)
        {
            var result = new ExcelUploadResult();

            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    result.Message = "Please select a file to upload";
                    return Json(result);
                }

                if (!Path.GetExtension(file.FileName).ToLower().EndsWith(".xlsx") &&
                    !Path.GetExtension(file.FileName).ToLower().EndsWith(".xls"))
                {
                    result.Message = "Please upload only Excel files (.xlsx or .xls)";
                    return Json(result);
                }

                if (file.Length > 10 * 1024 * 1024) // 10MB limit
                {
                    result.Message = "File size must be less than 10MB";
                    return Json(result);
                }

                _logger.LogInformation($"Processing Excel file for After Washing: {file.FileName}, Size: {file.Length} bytes");

                var excelData = await ProcessExcelFile(file);

                if (!excelData.Any())
                {
                    result.Message = "No valid data found in Excel file";
                    return Json(result);
                }

                // Validate all rows
                var validRows = new List<ExcelRowData>();
                foreach (var row in excelData)
                {
                    if (row.IsValid)
                    {
                        validRows.Add(row);
                    }
                    else
                    {
                        result.DetailedErrors.Add(new ExcelRowError
                        {
                            RowNumber = row.RowNumber,
                            Error = string.Join(", ", row.ValidationErrors),
                            RowData = $"Timestamp: {row.Timestamp}, FullQR: {row.FullQR}, KodeItem: {row.KodeItem}, JmlBox: {row.JmlBox}"
                        });
                    }
                }

                result.ProcessedRows = excelData.Count;
                result.ErrorRows = result.DetailedErrors.Count;

                if (!validRows.Any())
                {
                    result.Message = "No valid rows found to import";
                    result.Errors = result.DetailedErrors.Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                    return Json(result);
                }

                // Insert valid data to database (storage_log_aw)
                var insertedCount = await InsertStorageLogAWData(validRows);

                result.Success = true;
                result.SuccessfulRows = insertedCount;
                result.Message = $"Successfully imported {insertedCount} records from {result.ProcessedRows} total rows";

                if (result.ErrorRows > 0)
                {
                    result.Message += $" ({result.ErrorRows} rows had errors and were skipped)";
                    result.Errors = result.DetailedErrors.Take(10).Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                }

                _logger.LogInformation($"After Washing Excel upload completed: {insertedCount} successful, {result.ErrorRows} errors");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing After Washing Excel upload");
                result.Message = $"Error processing file: {ex.Message}";
                result.Errors.Add(ex.Message);
            }

            return Json(result);
        }

        private async Task<List<ExcelRowData>> ProcessExcelFile(IFormFile file)
        {
            var rows = new List<ExcelRowData>();

            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

                using var package = new OfficeOpenXml.ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0];

                var rowCount = worksheet.Dimension?.Rows ?? 0;
                var colCount = worksheet.Dimension?.Columns ?? 0;

                _logger.LogInformation($"After Washing Excel has {rowCount} rows and {colCount} columns");

                if (rowCount <= 1)
                {
                    _logger.LogWarning("Excel file has no data rows (only header or empty)");
                    return rows;
                }

                // Process data starting from row 2 - SAMA SEPERTI HomeController
                for (int row = 2; row <= rowCount; row++)
                {
                    var rowData = new ExcelRowData { RowNumber = row };

                    try
                    {
                        var timestampCell = worksheet.Cells[row, 1].Value?.ToString()?.Trim();      // A: Timestamp
                        var kodeRakCell = worksheet.Cells[row, 2].Value?.ToString()?.Trim();        // B: Kode Rak
                        var fullQRCell = worksheet.Cells[row, 3].Value?.ToString()?.Trim();         // C: Full QR
                        var kodeItemCell = worksheet.Cells[row, 4].Value?.ToString()?.Trim();       // D: Kode Item
                        var jmlBoxCell = worksheet.Cells[row, 5].Value?.ToString()?.Trim();         // E: Jml Box

                        // Skip completely empty rows
                        if (string.IsNullOrEmpty(timestampCell) && string.IsNullOrEmpty(fullQRCell) &&
                            string.IsNullOrEmpty(kodeItemCell) && string.IsNullOrEmpty(jmlBoxCell))
                        {
                            continue;
                        }

                        // Parse Timestamp (Required)
                        if (!string.IsNullOrEmpty(timestampCell))
                        {
                            DateTime timestamp;
                            bool timestampParsed = false;

                            // Try Excel date format first
                            if (worksheet.Cells[row, 1].Value is double excelDate)
                            {
                                try
                                {
                                    timestamp = DateTime.FromOADate(excelDate);
                                    rowData.Timestamp = timestamp;
                                    timestampParsed = true;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning($"Failed to parse Excel date {excelDate} at row {row}: {ex.Message}");
                                }
                            }

                            // Try standard DateTime parsing if Excel date failed
                            if (!timestampParsed)
                            {
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
                                    if (DateTime.TryParseExact(timestampCell, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out timestamp))
                                    {
                                        rowData.Timestamp = timestamp;
                                        timestampParsed = true;
                                        break;
                                    }
                                }
                            }

                            if (!timestampParsed)
                            {
                                rowData.ValidationErrors.Add($"Invalid timestamp format: {timestampCell}");
                            }
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("Timestamp is required");
                        }

                        // Read Kode Rak but don't validate (it's not stored in database)
                        rowData.KodeRak = kodeRakCell;

                        // Validate FullQR (Required)
                        if (!string.IsNullOrEmpty(fullQRCell))
                        {
                            if (fullQRCell.Length > 300)
                            {
                                rowData.ValidationErrors.Add("FullQR exceeds maximum length (300 characters)");
                            }
                            else
                            {
                                rowData.FullQR = fullQRCell;
                            }
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("FullQR is required");
                        }

                        // Validate KodeItem (Required)
                        if (!string.IsNullOrEmpty(kodeItemCell))
                        {
                            if (kodeItemCell.Length > 100)
                            {
                                rowData.ValidationErrors.Add("KodeItem exceeds maximum length (100 characters)");
                            }
                            else
                            {
                                rowData.KodeItem = kodeItemCell;
                            }
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("KodeItem is required");
                        }

                        // Parse JmlBox (Required)
                        if (!string.IsNullOrEmpty(jmlBoxCell))
                        {
                            if (int.TryParse(jmlBoxCell.Replace(",", "").Replace(".", ""), out int jmlBox))
                            {
                                if (jmlBox >= 0)
                                {
                                    rowData.JmlBox = jmlBox;
                                }
                                else
                                {
                                    rowData.ValidationErrors.Add("JmlBox must be a positive number or zero");
                                }
                            }
                            else
                            {
                                rowData.ValidationErrors.Add($"Invalid JmlBox format: {jmlBoxCell} (must be a number)");
                            }
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("JmlBox is required");
                        }

                        rows.Add(rowData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing row {row}");
                        rowData.ValidationErrors.Add($"Error processing row: {ex.Message}");
                        rows.Add(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Excel file");
                throw new Exception($"Error reading Excel file: {ex.Message}", ex);
            }

            _logger.LogInformation($"Processed {rows.Count} rows from After Washing Excel file");
            return rows;
        }

        private async Task<int> InsertStorageLogAWData(List<ExcelRowData> validRows)
        {
            var insertedCount = 0;

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    foreach (var row in validRows)
                    {
                        var storageLogAW = new StorageLogAW
                        {
                            ItemCode = row.KodeItem,
                            FullQr = row.FullQR,
                            StoredAt = row.Timestamp,
                            BoxCount = row.JmlBox,
                            Tanggal = row.Timestamp?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy"),
                            ProductionDate = null,
                            QtyPcs = null
                        };

                        _context.StorageLogAW.Add(storageLogAW);
                        insertedCount++;

                        if (insertedCount % 100 == 0)
                        {
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"After Washing batch saved: {insertedCount} records processed");
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation($"Successfully inserted {insertedCount} records into storage_log_aw table");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during After Washing database transaction, rolling back");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting data into storage_log_aw table");
                throw new Exception($"Database error: {ex.Message}", ex);
            }

            return insertedCount;
        }

        [HttpGet]
        public async Task<JsonResult> GetDashboardData()
        {
            try
            {
                var allItems = await _context.ItemAW.ToListAsync();
                var allStockSummaries = await _context.StockSummaryAW.ToListAsync();

                var stockSummaryGrouped = allStockSummaries
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0), // Sum ALL records
                        LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue
                    });

                var itemsWithStockData = allItems.Select(item => new
                {
                    Item = item,
                    TotalCurrentBoxStock = stockSummaryGrouped.ContainsKey(item.ItemCode)
                                          ? stockSummaryGrouped[item.ItemCode].TotalCurrentBoxStock
                                          : 0,
                    LastUpdated = stockSummaryGrouped.ContainsKey(item.ItemCode)
                                 ? stockSummaryGrouped[item.ItemCode].LastUpdated
                                 : DateTime.MinValue,
                    HasStockData = stockSummaryGrouped.ContainsKey(item.ItemCode)
                }).ToList();

                var filteredItems = itemsWithStockData;

                var dashboardSummary = new DashboardSummary
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => IsExpired(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => IsNearExpired(x.LastUpdated, x.Item.StandardExp)),
                    ShortageCount = filteredItems.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                return Json(dashboardSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting After Washing dashboard data via API");
                return Json(new { error = "Unable to load data", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTableData()
        {
            try
            {
                _logger.LogInformation("Loading After Washing table data for DataTables (Items-based)...");

                var allItems = await _context.ItemAW.ToListAsync();
                var allStockSummaries = await _context.StockSummaryAW.ToListAsync();

                var stockSummaryGrouped = allStockSummaries
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0), // Sum ALL records
                        LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue,
                        RecordCount = g.Count()
                    });

                var tableData = allItems.Select((item, index) =>
                {
                    var hasStockData = stockSummaryGrouped.ContainsKey(item.ItemCode);
                    var stockData = hasStockData ? stockSummaryGrouped[item.ItemCode] : null;
                    var currentBoxStock = stockData?.TotalCurrentBoxStock ?? 0;
                    var lastUpdated = stockData?.LastUpdated ?? DateTime.MinValue;

                    return new
                    {
                        No = index + 1,
                        ItemCode = item.ItemCode,
                        StandardMin = item.StandardMin,
                        StandardMax = item.StandardMax,
                        CurrentBoxStock = currentBoxStock,
                        Pcs = item.QtyPerBox.HasValue ?
                              Math.Round((decimal)currentBoxStock * item.QtyPerBox.Value, 2) : 0,
                        Status = DetermineItemStatus(currentBoxStock, lastUpdated, item),
                        LastUpdatedDate = lastUpdated,
                        StandardExp = item.StandardExp,
                        QtyPerBox = item.QtyPerBox,
                        HasStockData = hasStockData,
                        StockRecordCount = stockData?.RecordCount ?? 0
                    };
                })
                .OrderBy(x => x.ItemCode)
                .ToList();

                _logger.LogInformation($"Prepared {tableData.Count} After Washing records for table display (Items-based)");

                return Json(new { data = tableData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading After Washing table data (Items-based)");
                return Json(new { error = "Unable to load table data", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetItemsWithStockComparison()
        {
            try
            {
                var allItems = await _context.ItemAW.ToListAsync();
                var allStockSummaries = await _context.StockSummaryAW.ToListAsync();

                var stockSummaryGrouped = allStockSummaries
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0), // Sum ALL records
                        LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue,
                        RecordCount = g.Count(),
                        Records = g.ToList()
                    });

                var comparison = new
                {
                    TotalItemsInItemsAWTable = allItems.Count,
                    TotalRecordsInStockAWView = allStockSummaries.Count,
                    UniqueItemCodesInStockAWView = stockSummaryGrouped.Count,
                    ItemsWithStockData = allItems.Count(i => stockSummaryGrouped.ContainsKey(i.ItemCode)),
                    ItemsWithoutStockData = allItems.Count(i => !stockSummaryGrouped.ContainsKey(i.ItemCode)),
                    StockDataWithoutItemMaster = stockSummaryGrouped.Count(kvp => !allItems.Any(i => i.ItemCode == kvp.Key)),

                    ItemsWithoutStock = allItems
                        .Where(i => !stockSummaryGrouped.ContainsKey(i.ItemCode))
                        .Take(10)
                        .Select(i => new { i.ItemCode, i.Mesin })
                        .ToList(),

                    StockWithoutItem = stockSummaryGrouped
                        .Where(kvp => !allItems.Any(i => i.ItemCode == kvp.Key))
                        .Take(10)
                        .Select(kvp => new
                        {
                            ItemCode = kvp.Key,
                            TotalStock = kvp.Value.TotalCurrentBoxStock,
                            RecordCount = kvp.Value.RecordCount
                        })
                        .ToList()
                };

                return Json(new { success = true, comparison });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Helper methods - SAMA SEPERTI HomeController
        private string DetermineItemStatus(int currentBoxStock, DateTime lastUpdated, ItemAW item)
        {
            // Jika stok box dan pcs adalah 0, anggap sebagai Out of Stock atau Normal
            if (currentBoxStock == 0 && (item.QtyPerBox.HasValue && currentBoxStock * item.QtyPerBox.Value == 0))
            {
                return "Out of Stock"; // Atau "Normal" sesuai kebutuhan
            }

            // Prioritas 1: Check for expired (hanya jika stok > 0)
            if (currentBoxStock > 0 && IsExpired(lastUpdated, item.StandardExp))
                return "Already Expired";

            // Prioritas 2: Check for near expired (hanya jika stok > 0)
            if (currentBoxStock > 0 && IsNearExpired(lastUpdated, item.StandardExp))
                return "Near Expired";

            // Prioritas 3: Check stock levels
            if (IsShortage(currentBoxStock, item.StandardMin))
                return "Shortage";

            if (IsAboveMax(currentBoxStock, item.StandardMax))
                return "Over Stock";

            return "Normal";
        }

        private double CalculateDaysUntilExpiry(DateTime lastUpdated, int? standardExp)
        {
            if (!standardExp.HasValue || lastUpdated == DateTime.MinValue) return 0;

            var daysSinceLastUpdate = (DateTime.Now - lastUpdated).TotalDays;
            return standardExp.Value - daysSinceLastUpdate;
        }

        private bool IsExpired(DateTime lastUpdated, int? standardExp)
        {
            if (!standardExp.HasValue || lastUpdated == DateTime.MinValue) return false;

            var daysUntilExpiry = CalculateDaysUntilExpiry(lastUpdated, standardExp);
            return daysUntilExpiry < 0;
        }

        private bool IsNearExpired(DateTime lastUpdated, int? standardExp)
        {
            if (!standardExp.HasValue || lastUpdated == DateTime.MinValue) return false;

            var daysUntilExpiry = CalculateDaysUntilExpiry(lastUpdated, standardExp);
            return daysUntilExpiry >= 0 && daysUntilExpiry <= 3;
        }

        private bool IsShortage(int currentStock, int? standardMin)
        {
            if (!standardMin.HasValue) return false;
            return currentStock >= 0 && currentStock <= standardMin.Value;
        }

        private bool IsBelowMin(int totalStock, int? standardMin)
        {
            return false; // Not used, replaced by IsShortage
        }

        private bool IsAboveMax(int totalStock, int? standardMax)
        {
            return standardMax.HasValue && totalStock > standardMax.Value;
        }

        // After Washing Input Page
        public async Task<IActionResult> AfterWashingInput()
        {
            ViewData["Title"] = "After Washing Input";
            return View();
        }

        // Submit After Washing Input (IN/SISA)
        [HttpPost]
        public async Task<JsonResult> SubmitAfterWashingInput([FromBody] AfterWashingInputModel model)
        {
            try
            {
                if (model == null)
                {
                    return Json(new { success = false, message = "Invalid data" });
                }

                // Validate required fields
                if (string.IsNullOrEmpty(model.ItemCode) || string.IsNullOrEmpty(model.FullQR))
                {
                    return Json(new { success = false, message = "Item Code and Full QR are required" });
                }

                // Both IN and SISA go to storage_log_aw
                var storageLogAW = new StorageLogAW
                {
                    ItemCode = model.ItemCode,
                    FullQr = model.FullQR,
                    StoredAt = DateTime.Now,
                    BoxCount = model.BoxCount ?? 0,
                    Tanggal = DateTime.Now.ToString("dd/MM/yyyy"),
                    ProductionDate = model.ProductionDate,
                    QtyPcs = model.QtyPcs
                };

                _context.StorageLogAW.Add(storageLogAW);
                await _context.SaveChangesAsync();

                var transactionLabel = model.TransactionType == "SISA" ? "SISA" : "IN";
                return Json(new { success = true, message = $"Data successfully saved ({transactionLabel})" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting After Washing input");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}