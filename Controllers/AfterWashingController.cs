using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;
using System.Diagnostics;
using System.Globalization;

namespace dashboardWIPHouse.Controllers
{
    [Authorize]
    public class AfterWashingController : Controller
    {
        private readonly ILogger<AfterWashingController> _logger;
        private readonly ApplicationDbContext _context;

        public AfterWashingController(ILogger<AfterWashingController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [Authorize(Roles = "Admin")]
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
                        StatusExpired = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.StatusExpired,
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

                // 6. Calculate dashboard summary with mutually exclusive status (same as table logic)
                var dashboardSummary = new DashboardSummary
                {
                    TotalItems = filteredItems.Count
                };

                foreach (var item in filteredItems)
                {
                    // Use the same helper method as the table for consistent status
                    string status = DetermineItemStatus(item.TotalCurrentBoxStock, item.LastUpdated, item.Item, item.StockData?.StatusExpired);

                    switch (status)
                    {
                        case "Already Expired":
                            dashboardSummary.ExpiredCount++;
                            break;
                        case "Near Expired":
                            dashboardSummary.NearExpiredCount++;
                            break;
                        case "Shortage":
                            dashboardSummary.ShortageCount++;
                            break;
                        case "Over Stock":
                            dashboardSummary.AboveMaxCount++;
                            break;
                    }
                }

                // Detailed logging for verification
                _logger.LogInformation($"After Washing Dashboard Summary - Items Based (Consistent with Table):");
                _logger.LogInformation($"- Total Items: {dashboardSummary.TotalItems}");
                _logger.LogInformation($"- Already Expired: {dashboardSummary.ExpiredCount}");
                _logger.LogInformation($"- Near Expired: {dashboardSummary.NearExpiredCount}");
                _logger.LogInformation($"- Shortage: {dashboardSummary.ShortageCount}");
                _logger.LogInformation($"- Over Stock: {dashboardSummary.AboveMaxCount}");

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

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadTemplate()
        {
            try
            {
                _logger.LogInformation("Generating After Washing Excel template");

                OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using var package = new OfficeOpenXml.ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Template");

                // Headers
                worksheet.Cells[1, 1].Value = "Timestamp";
                worksheet.Cells[1, 2].Value = "Kode Rak";
                worksheet.Cells[1, 3].Value = "Full QR";
                worksheet.Cells[1, 4].Value = "Kode Item";
                worksheet.Cells[1, 5].Value = "Jml Box";

                // Add sample data
                worksheet.Cells[2, 1].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cells[2, 2].Value = "RAK-AW-01";
                worksheet.Cells[2, 3].Value = "AW-ITEM001-B12-1";
                worksheet.Cells[2, 4].Value = "ITEM001";
                worksheet.Cells[2, 5].Value = 10;

                // Style the header
                using (var range = worksheet.Cells[1, 1, 1, 5])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                }

                worksheet.Cells.AutoFitColumns();

                var fileName = $"AfterWashing_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                var fileBytes = package.GetAsByteArray();

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating After Washing Excel template");
                return BadRequest($"Error generating template: {ex.Message}");
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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
                             .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue,
                StatusExpired = g.Where(s => s.ParsedLastUpdated.HasValue)
                             .OrderByDescending(s => s.ParsedLastUpdated)
                             .FirstOrDefault()?.StatusExpired
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
            StatusExpired = stockSummaryGrouped.ContainsKey(item.ItemCode)
                         ? stockSummaryGrouped[item.ItemCode].StatusExpired
                         : null,
            HasStockData = stockSummaryGrouped.ContainsKey(item.ItemCode)
        }).ToList();

        var dashboardSummary = new DashboardSummary
        {
            TotalItems = itemsWithStockData.Count,
            ExpiredCount = 0,
            NearExpiredCount = 0,
            ShortageCount = 0,
            BelowMinCount = 0, // Assuming BelowMinCount is not directly derived from DetermineItemStatus's main cases
            AboveMaxCount = 0
        };

        foreach (var item in itemsWithStockData)
        {
            // Use consistent status helper
            string status = DetermineItemStatus(item.TotalCurrentBoxStock, item.LastUpdated, item.Item, item.StatusExpired);

            switch (status)
            {
                case "Already Expired":
                    dashboardSummary.ExpiredCount++;
                    break;
                case "Near Expired":
                    dashboardSummary.NearExpiredCount++;
                    break;
                case "Shortage":
                    dashboardSummary.ShortageCount++;
                    break;
                case "Over Stock": // This corresponds to AboveMaxCount
                    dashboardSummary.AboveMaxCount++;
                    break;
                case "Out of Stock": // Out of Stock implies BelowMin, but not necessarily Shortage if min is 0
                    dashboardSummary.BelowMinCount++;
                    break;
                case "Normal":
                    // If it's normal, check for BelowMin specifically if not already caught by Shortage
                    if (IsBelowMin(item.TotalCurrentBoxStock, item.Item.StandardMin))
                    {
                        dashboardSummary.BelowMinCount++;
                    }
                    break;
            }
            // The original logic for BelowMinCount was separate.
            // If Shortage is defined as currentStock < standardMin, then Shortage implies BelowMin.
            // If BelowMin is meant to be a broader category (e.g., currentStock <= standardMin),
            // and Shortage is currentStock < standardMin, then we need to be careful.
            // For now, I'll assume "Shortage" covers the primary "below min" case,
            // and "Out of Stock" is also a "below min" case.
            // If "BelowMinCount" needs to be distinct from "ShortageCount" and "Out of Stock",
            // the definition of IsBelowMin needs to be re-evaluated against IsShortage.
            // Based on the original code, IsShortage is currentStock < standardMin,
            // and IsBelowMin is also currentStock < standardMin. They are identical.
            // So, if an item is "Shortage", it is also "BelowMin".
            // The original code had:
            // ShortageCount = filteredItems.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)),
            // BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
            // This means ShortageCount and BelowMinCount would be identical if IsShortage and IsBelowMin are identical.
            // Let's align with the `DetermineItemStatus` logic.
            // `DetermineItemStatus` has "Shortage" as a status. It does not have a "BelowMin" status.
            // If "BelowMinCount" is intended to be the same as "ShortageCount", then we can remove the separate increment.
            // If "BelowMinCount" is meant to capture items that are at min but not below, or if it's just a duplicate of shortage,
            // then the original code was redundant.
            // For now, I'll make BelowMinCount increment if it's "Out of Stock" or "Normal" but still below min.
            // This makes it distinct from "Shortage" if "Shortage" is only for items *strictly* below min.
            // However, looking at the helper methods:
            // private bool IsShortage(int currentStock, int? standardMin)
            // private bool IsBelowMin(int currentStock, int? standardMin)
            // These are likely identical. If so, the original dashboard had duplicate counts.
            // I will assume "Shortage" covers the "below min" scenario.
            // If "BelowMinCount" is truly needed as a separate metric, its definition needs to be clarified.
            // For now, I will remove the explicit `BelowMinCount` increment in the loop,
            // as `Shortage` and `Out of Stock` already cover the "below minimum" scenarios.
            // If `IsBelowMin` is distinct, it should be called.
            // Let's re-check the helper methods.
            // IsShortage(currentStock, item.StandardMin)
            // IsBelowMin(currentStock, item.StandardMin)
            // The user provided only the signature for IsShortage, not IsBelowMin.
            // Assuming IsBelowMin is defined similarly to IsShortage, they might be identical.
            // To be safe and match the original intent of having a `BelowMinCount`,
            // I will add a check for `IsBelowMin` if the status is not already `Shortage` or `Out of Stock`.
            // This implies `IsBelowMin` might have a slightly different condition or is a broader category.
            // Given the instruction is to fix inconsistent logic, and `DetermineItemStatus` doesn't return "BelowMin",
            // I should ensure `BelowMinCount` is still calculated.
            // The original code calculated `BelowMinCount` directly using `IsBelowMin`.
            // Let's re-add a check for `IsBelowMin` for items that are not already categorized as Shortage or Out of Stock.
            // This ensures `BelowMinCount` is still populated, even if its definition overlaps with `Shortage`.
            // If `IsBelowMin` is identical to `IsShortage`, then `BelowMinCount` will be the same as `ShortageCount` + `Out of Stock` count.
            // Let's assume `IsBelowMin` is intended to be distinct or a superset.
            if (status != "Shortage" && status != "Out of Stock" && IsBelowMin(item.TotalCurrentBoxStock, item.Item.StandardMin))
            {
                dashboardSummary.BelowMinCount++;
            }
        }

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
                        // Get status_expired from the most recent record
                        StatusExpired = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.StatusExpired,
                        RecordCount = g.Count()
                    });

                var tableData = allItems.Select((item, index) =>
                {
                    var hasStockData = stockSummaryGrouped.ContainsKey(item.ItemCode);
                    var stockData = hasStockData ? stockSummaryGrouped[item.ItemCode] : null;
                    var currentBoxStock = stockData?.TotalCurrentBoxStock ?? 0;
                    var lastUpdated = stockData?.LastUpdated ?? DateTime.MinValue;
                    var statusExpired = stockData?.StatusExpired; // Get from database

                    return new
                    {
                        No = index + 1,
                        ItemCode = item.ItemCode,
                        StandardMin = item.StandardMin,
                        StandardMax = item.StandardMax,
                        CurrentBoxStock = currentBoxStock,
                        Pcs = item.QtyPerBox.HasValue ?
                              Math.Round((decimal)currentBoxStock * item.QtyPerBox.Value, 2) : 0,
                        Status = DetermineItemStatus(currentBoxStock, lastUpdated, item, statusExpired),
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

        // Helper methods - Now uses status_expired from database when available
        private string DetermineItemStatus(int currentBoxStock, DateTime lastUpdated, ItemAW item, string? statusExpired = null)
        {
            string displayStatus = "Normal";

            // Prioritas 1: Cek dari Database Status Expired
            if (!string.IsNullOrEmpty(statusExpired))
            {
                if (statusExpired.Contains("expired", StringComparison.OrdinalIgnoreCase))
                {
                    displayStatus = "Already Expired";
                }
                else if (statusExpired.Contains("hampir", StringComparison.OrdinalIgnoreCase) || 
                         statusExpired.Contains("near", StringComparison.OrdinalIgnoreCase))
                {
                    displayStatus = "Near Expired";
                }
            }

            // Prioritas 2: Manual Check (jika status masih Normal)
            // Note: Green Hose logic checks expiry FIRST even if stock is 0? 
            // Actually Green Hose checks (stock > 0 && IsExpired) -> Expired.
            // If stock <= 0, it falls through to Shortage check.
            if (displayStatus == "Normal" && currentBoxStock > 0)
            {
                if (IsExpired(lastUpdated, item.StandardExp))
                    displayStatus = "Already Expired";
                else if (IsNearExpired(lastUpdated, item.StandardExp))
                    displayStatus = "Near Expired";
            }

            // Prioritas 3: Stock Levels
            // Merge "Out of Stock" (<=0) and "Shortage" (< Min) into single "Shortage" status
            if (displayStatus == "Normal")
            {
                // Check Shortage first (includes 0 stock if Min is set)
                if (IsShortage(currentBoxStock, item.StandardMin))
                    displayStatus = "Shortage";
                else if (IsAboveMax(currentBoxStock, item.StandardMax))
                    displayStatus = "Over Stock";
            }

            return displayStatus;
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
            // Consistent with Green Hose: 
            // If Min is not set, no shortage.
            // If Min is set, any stock <= Min is shortage (including 0 <= 0).
            // But wait, if Min is 0, stock 0 is Normal?
            // Green Hose: return currentStock >= 0 && currentStock <= standardMin.Value;
            
            if (!standardMin.HasValue) return false;
            
            // If Standard Min is 0, we generally don't flag shortage unless we want to flag empty items that explicitly have Min=0 (rare)
            // But strict <= logic:
            if (standardMin.Value == 0 && currentStock == 0) return false; // Treat 0/0 as Normal
            
            return currentStock <= standardMin.Value;
        }

        private bool IsBelowMin(int totalStock, int? standardMin)
        {
            return IsShortage(totalStock, standardMin);
        }

        private bool IsAboveMax(int totalStock, int? standardMax)
        {
            return standardMax.HasValue && standardMax.Value > 0 && totalStock > standardMax.Value;
        }

        // After Washing Input Page
        public async Task<IActionResult> AfterWashingInput()
        {
            ViewData["Title"] = "After Washing Input";
            return View();
        }

        // Get Item Codes for Autocomplete
        [HttpGet]
        public async Task<JsonResult> GetItemCodes(string search = "")
        {
            try
            {
                var query = _context.ItemAW.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(i => i.ItemCode.Contains(search));
                }

                var itemCodes = await query
                    .Select(i => new { id = i.ItemCode, text = i.ItemCode })
                    .Take(50)
                    .ToListAsync();

                return Json(new { results = itemCodes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting item codes");
                return Json(new { results = new List<object>() });
            }
        }

        // Get Full QR Codes for Autocomplete
        [HttpGet]
        public async Task<JsonResult> GetFullQRCodes(string search = "")
        {
            try
            {
                var query = _context.RaksAW.AsQueryable();

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(r => r.FullQR.Contains(search));
                }

                var fullQRCodes = await query
                    .Select(r => new { id = r.FullQR, text = r.FullQR })
                    .Distinct()
                    .Take(50)
                    .ToListAsync();

                return Json(new { results = fullQRCodes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting full QR codes");
                return Json(new { results = new List<object>() });
            }
        }

        // Submit After Washing Input (IN/SISA/OUT)
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

        if (model.TransactionType == "OUT")
        {
            // Save to supply_log_aw
            var supplyLogAW = new SupplyLogAW
            {
                ItemCode = model.ItemCode,
                FullQr = model.FullQR,
                SuppliedAt = DateTime.Now,
                BoxCount = model.BoxCount ?? 0,
                Tanggal = DateTime.Now.ToString("dd/MM/yyyy"),
                QtyPcs = model.QtyPcs
            };
            _context.SupplyLogAW.Add(supplyLogAW);
        }
        else
        {
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
        }

        await _context.SaveChangesAsync();

        string transactionLabel = model.TransactionType;
        return Json(new { success = true, message = $"Data successfully saved ({transactionLabel})" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error submitting After Washing input");
        return Json(new { success = false, message = $"Error: {ex.Message}" });
    }
}
        [Authorize]
        public IActionResult History()
        {
            return View("AfterWashingHistory");
        }

        [HttpGet]
        public async Task<JsonResult> GetHistoryData(string type, DateTime? date = null)
        {
            try
            {
                if (type == "in")
                {
                    var query = _context.StorageLogAW.AsQueryable();

                    if (date.HasValue)
                    {
                        var targetDate = date.Value.Date;
                        query = query.Where(x => x.StoredAt.HasValue && x.StoredAt.Value.Date == targetDate);
                    }

                    var data = await query
                        .OrderByDescending(x => x.StoredAt)
                        .Take(100)
                        .Select(x => new {
                            date = x.StoredAt.HasValue ? x.StoredAt.Value.ToString("dd-MM-yyyy HH:mm") : "-",
                            item = x.ItemCode,
                            qty = x.BoxCount ?? 0,
                            qtyPcs = x.QtyPcs ?? 0,
                            status = "IN",
                            id = x.LogId
                        })
                        .ToListAsync();
                    return Json(new { success = true, data });
                }
                else if (type == "out")
                {
                    var query = _context.SupplyLogAW.AsQueryable();

                    if (date.HasValue)
                    {
                        var targetDate = date.Value.Date;
                        query = query.Where(x => x.SuppliedAt.HasValue && x.SuppliedAt.Value.Date == targetDate);
                    }

                    var data = await query
                        .OrderByDescending(x => x.SuppliedAt)
                        .Take(100)
                        .Select(x => new {
                            date = x.SuppliedAt.HasValue ? x.SuppliedAt.Value.ToString("dd-MM-yyyy HH:mm") : "-",
                            item = x.ItemCode,
                            qty = x.BoxCount ?? 0, // Using Box Count for Qty
                            qtyPcs = x.QtyPcs ?? 0,
                            status = "OUT",
                            id = x.LogId
                        })
                        .ToListAsync();

                    return Json(new { success = true, data });
                }
                else if (type == "stock")
                {
                    var allStock = await _context.StockSummaryAW.ToListAsync();
                    var allItems = await _context.ItemAW.ToDictionaryAsync(i => i.ItemCode.Trim(), StringComparer.OrdinalIgnoreCase);
                    
                    var grouped = allStock
                        .Where(x => !string.IsNullOrEmpty(x.ItemCode))
                        .GroupBy(x => x.ItemCode.Trim())
                        .Select(g => {
                            // Logic untuk ambil status terbaru
                            var latestRecord = g.Where(s => s.ParsedLastUpdated.HasValue)
                                               .OrderByDescending(s => s.ParsedLastUpdated)
                                               .FirstOrDefault();

                            var totalStock = g.Sum(x => x.CurrentBoxStock ?? 0);
                            var statusExpired = latestRecord?.StatusExpired;
                            var lastUpdated = latestRecord?.ParsedLastUpdated ?? DateTime.MinValue;
                            
                            // Get Item info
                            allItems.TryGetValue(g.Key, out var item);

                            // Calculate Pcs
                            var totalPcs = item?.QtyPerBox.HasValue == true ? 
                                           Math.Round((decimal)totalStock * item.QtyPerBox.Value, 0) : 0;

                            // Determine display status
                            string displayStatus = "Normal";
                            if (item != null) 
                            {
                                displayStatus = DetermineItemStatus(totalStock, lastUpdated, item, statusExpired);
                            }
                            else if (!string.IsNullOrEmpty(statusExpired))
                            {
                                if (statusExpired.Equals("expired", StringComparison.OrdinalIgnoreCase)) displayStatus = "Already Expired";
                                else if (statusExpired.Contains("hampir", StringComparison.OrdinalIgnoreCase)) displayStatus = "Near Expired";
                                else if (statusExpired.Equals("ok", StringComparison.OrdinalIgnoreCase)) displayStatus = "Normal";
                                else displayStatus = statusExpired; 
                            }

                            return new {
                                itemCode = g.Key,
                                description = displayStatus,
                                qty = totalStock,
                                qtyPcs = totalPcs,
                                lastUpdated = lastUpdated != DateTime.MinValue ? lastUpdated.ToString("dd-MM-yyyy HH:mm") : "-"
                            };
                        })
                        .OrderBy(x => x.itemCode)
                        .ToList();
                    return Json(new { success = true, data = grouped });
                }
                return Json(new { success = false, message = "Invalid type" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }

        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<JsonResult> DeleteHistory(int id, string type)
        {
            try
            {
                if (type == "in")
                {
                    var log = await _context.StorageLogAW.FindAsync(id);
                    if (log == null) return Json(new { success = false, message = "Record not found" });
                    _context.StorageLogAW.Remove(log);
                }
                else if (type == "out")
                {
                    var log = await _context.SupplyLogAW.FindAsync(id);
                    if (log == null) return Json(new { success = false, message = "Record not found" });
                    _context.SupplyLogAW.Remove(log);
                }
                else
                {
                    return Json(new { success = false, message = "Type not supported for deletion" });
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Record deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting history");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // Planning Finishing Page
        [HttpGet]
        public IActionResult Planning()
        {
            return View();
        }

        // Get Planning Data API
        [HttpGet]
        public async Task<JsonResult> GetPlanningData(string? mesin = null)
        {
            try
            {
                var query = _context.PlanningFinishing.AsQueryable();

                // Filter by mesin if provided
                if (!string.IsNullOrEmpty(mesin) && mesin != "Semua Mesin")
                {
                    query = query.Where(p => p.NoMesin == mesin);
                }

                // Get current date to filter only relevant planning
                var today = DateTime.Now.Date;

                var planningData = await query
                    .OrderBy(p => p.LoadTime)
                    .ThenBy(p => p.NoMesin)
                    .Select(p => new
                    {
                        mesin = p.NoMesin,
                        itemCode = p.KodeItem,
                        qty = p.QtyPlan,
                        tanggal = p.LoadTime.HasValue ? p.LoadTime.Value.ToString("yyyy-MM-dd") : null,
                        shift = p.Shift,
                        keterangan = p.Keterangan
                    })
                    .Take(100) // Limit to 100 recent records
                    .ToListAsync();

                // Get unique mesin list for filter
                var mesinList = await _context.PlanningFinishing
                    .Where(p => p.NoMesin != null)
                    .Select(p => p.NoMesin)
                    .Distinct()
                    .OrderBy(m => m)
                    .ToListAsync();

                return Json(new { success = true, data = planningData, mesinList });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting planning data");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}