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
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            try
            {
                _logger.LogInformation("=== Dashboard Load Started (Items-based) ===");

                // 1. Test koneksi database
                _logger.LogInformation("Testing database connection...");
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation($"Database connection result: {canConnect}");

                if (!canConnect)
                {
                    throw new Exception("Cannot connect to database");
                }

                // 2. Load data dari Items dan StockSummary
                _logger.LogInformation("Loading Items data...");
                var allItems = await _context.Items.ToListAsync();
                _logger.LogInformation($"Loaded {allItems.Count} items from Items table");

                _logger.LogInformation("Loading Stock Summary data...");
                var allStockSummaries = await _context.StockSummary.ToListAsync();
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from vw_stock_summary");

                // 3. Group stock summaries by item_code untuk aggregation
                // FINAL CORRECT LOGIC: Sum ALL records with same item_code (no deduplication)
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
                        // Get status_expired from the most recent record
                        StatusExpired = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.StatusExpired,
                        RecordCount = g.Count(),
                        Records = g.ToList(), // Keep all records for debugging
                        UniqueFullQRCount = g.Select(r => r.FullQr).Distinct().Count()
                    });

                // Debug logging for TA1240 and TA1400 specifically
                if (stockSummaryGrouped.ContainsKey("TA1240"))
                {
                    var ta1240Data = stockSummaryGrouped["TA1240"];
                    _logger.LogInformation($"TA1240 Debug - Total Records: {ta1240Data.RecordCount}, Unique FullQRs: {ta1240Data.UniqueFullQRCount}, Total Stock: {ta1240Data.TotalCurrentBoxStock}");
                    foreach (var record in ta1240Data.Records)
                    {
                        _logger.LogInformation($"TA1240 Record - FullQR: {record.FullQr}, CurrentBoxStock: {record.CurrentBoxStock}, LastUpdated: {record.LastUpdated}");
                    }
                }
                
                if (stockSummaryGrouped.ContainsKey("TA1400"))
                {
                    var ta1400Data = stockSummaryGrouped["TA1400"];
                    _logger.LogInformation($"TA1400 Debug - Total Records: {ta1400Data.RecordCount}, Unique FullQRs: {ta1400Data.UniqueFullQRCount}, Total Stock: {ta1400Data.TotalCurrentBoxStock}");
                    foreach (var record in ta1400Data.Records)
                    {
                        _logger.LogInformation($"TA1400 Record - LogId: {record.LogId}, FullQR: {record.FullQr}, CurrentBoxStock: {record.CurrentBoxStock}, LastUpdated: {record.LastUpdated}");
                    }
                }

                _logger.LogInformation($"Aggregated stock data for {stockSummaryGrouped.Count} unique item codes");

                // 4. Combine Items dengan Stock Summary data
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

                // 5. Filter items berdasarkan kriteria tertentu (optional)
                var filteredItems = itemsWithStockData.ToList();

                // 6. Calculate dashboard summary dengan status baru
                var dashboardSummary = new DashboardSummary
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => x.TotalCurrentBoxStock > 0 && IsExpired(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => x.TotalCurrentBoxStock > 0 && IsNearExpired(x.LastUpdated, x.Item.StandardExp)),
                    ShortageCount = filteredItems.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                // 7. Detailed logging for verification
                _logger.LogInformation($"Dashboard Summary - Items Based:");
                _logger.LogInformation($"- Total Items: {dashboardSummary.TotalItems}");
                _logger.LogInformation($"- Expired: {dashboardSummary.ExpiredCount}");
                _logger.LogInformation($"- Near Expired: {dashboardSummary.NearExpiredCount}");
                _logger.LogInformation($"- Shortage: {dashboardSummary.ShortageCount}");
                _logger.LogInformation($"- Below Min: {dashboardSummary.BelowMinCount}");
                _logger.LogInformation($"- Above Max: {dashboardSummary.AboveMaxCount}");

                // Log beberapa contoh items untuk debugging
                var sampleItems = filteredItems.Take(5).ToList();
                foreach (var item in sampleItems)
                {
                    _logger.LogInformation($"Sample - {item.Item.ItemCode}: Stock={item.TotalCurrentBoxStock}, " +
                        $"Min={item.Item.StandardMin}, Max={item.Item.StandardMax}, Status={DetermineItemStatus(item.TotalCurrentBoxStock, item.LastUpdated, item.Item)}");
                }

                ViewData["Title"] = "Dashboard";
                _logger.LogInformation("=== Dashboard Load Completed Successfully (Items-based) ===");
                return View(dashboardSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError($"=== Dashboard Load Failed ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                var emptySummary = new DashboardSummary();
                ViewData["Title"] = "Dashboard";
                ViewData["Error"] = $"Unable to load dashboard data: {ex.Message}";
                return View(emptySummary);
            }
        }
        
        [HttpPost]
        [Authorize(Roles = "Admin")]
public async Task<JsonResult> UploadExcel(IFormFile file, string uploadType = "storage")
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

        _logger.LogInformation($"Processing Excel file: {file.FileName}, Size: {file.Length} bytes, Upload Type: {uploadType}");

        int insertedCount = 0;

        if (uploadType == "supply")
        {
            // Process for Supply Log
            var excelDataSupply = await ProcessExcelFileSupply(file);
            
            if (!excelDataSupply.Any())
            {
                result.Message = "No valid data found in Excel file";
                return Json(result);
            }

            // Validate all rows
            var validRowsSupply = new List<ExcelRowDataSupply>();
            foreach (var row in excelDataSupply)
            {
                if (row.IsValid)
                {
                    validRowsSupply.Add(row);
                }
                else
                {
                    result.DetailedErrors.Add(new ExcelRowError
                    {
                        RowNumber = row.RowNumber,
                        Error = string.Join(", ", row.ValidationErrors),
                        RowData = $"ItemCode: {row.ItemCode}, FullQR: {row.FullQR}, BoxCount: {row.BoxCount}, QtyPcs: {row.QtyPcs}, SuppliedAt: {row.SuppliedAt}, ToProcess: {row.ToProcess}"
                    });
                }
            }

            result.ProcessedRows = excelDataSupply.Count;
            result.ErrorRows = result.DetailedErrors.Count;

            if (!validRowsSupply.Any())
            {
                result.Message = "No valid rows found to import";
                result.Errors = result.DetailedErrors.Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                return Json(result);
            }

            // Insert valid data to database
            insertedCount = await InsertSupplyLogData(validRowsSupply);
        }
        else if (uploadType == "raks")
        {
            // Process for Raks
            var excelDataRaks = await ProcessExcelFileRaks(file);
            
            if (!excelDataRaks.Any())
            {
                result.Message = "No valid data found in Excel file";
                return Json(result);
            }

            // Validate all rows
            var validRowsRaks = new List<ExcelRowDataRaks>();
            foreach (var row in excelDataRaks)
            {
                if (row.IsValid)
                {
                    validRowsRaks.Add(row);
                }
                else
                {
                    result.DetailedErrors.Add(new ExcelRowError
                    {
                        RowNumber = row.RowNumber,
                        Error = string.Join(", ", row.ValidationErrors),
                        RowData = $"FullQR: {row.FullQR}, Location: {row.Location}, ItemCode: {row.ItemCode}"
                    });
                }
            }

            result.ProcessedRows = excelDataRaks.Count;
            result.ErrorRows = result.DetailedErrors.Count;

            if (!validRowsRaks.Any())
            {
                result.Message = "No valid rows found to import";
                result.Errors = result.DetailedErrors.Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                return Json(result);
            }

            // Insert valid data to database
            insertedCount = await InsertRaksData(validRowsRaks);
        }
        else
        {
            // Process for Storage Log (existing logic)
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
                        RowData = $"Timestamp: {row.Timestamp}, FullQR: {row.FullQR}, KodeItem: {row.KodeItem}, JmlBox: {row.JmlBox}, ProductionDate: {row.ProductionDate}, QtyPcs: {row.QtyPcs}"
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

            // Insert valid data to database
            insertedCount = await InsertStorageLogData(validRows);
        }
        
        result.Success = true;
        result.SuccessfulRows = insertedCount;
        result.Message = $"Successfully imported {insertedCount} records from {result.ProcessedRows} total rows";
        
        if (result.ErrorRows > 0)
        {
            result.Message += $" ({result.ErrorRows} rows had errors and were skipped)";
            result.Errors = result.DetailedErrors.Take(10).Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
        }

        _logger.LogInformation($"Excel upload completed: {insertedCount} successful, {result.ErrorRows} errors");

    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing Excel upload");
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

        _logger.LogInformation($"Excel has {rowCount} rows and {colCount} columns");

        if (rowCount <= 1)
        {
            _logger.LogWarning("Excel file has no data rows (only header or empty)");
            return rows;
        }

        // Process data starting from row 2
        // A=Timestamp, B=Kode Rak (skip), C=Full QR, D=Kode Item, E=Jml Box, F=Production Date, G=Qty Pcs
        for (int row = 2; row <= rowCount; row++)
        {
            var rowData = new ExcelRowData { RowNumber = row };

            try
            {
                var timestampCell = worksheet.Cells[row, 1].Value?.ToString()?.Trim(); // A: Timestamp
                var kodeRakCell = worksheet.Cells[row, 2].Value?.ToString()?.Trim();    // B: Kode Rak (read but don't validate)
                var fullQRCell = worksheet.Cells[row, 3].Value?.ToString()?.Trim();     // C: Full QR
                var kodeItemCell = worksheet.Cells[row, 4].Value?.ToString()?.Trim();   // D: Kode Item
                var jmlBoxCell = worksheet.Cells[row, 5].Value?.ToString()?.Trim();     // E: Jml Box
                var productionDateCell = worksheet.Cells[row, 6].Value?.ToString()?.Trim(); // F: Production Date
                var qtyPcsCell = worksheet.Cells[row, 7].Value?.ToString()?.Trim();     // G: Qty Pcs

                // Skip completely empty rows (check required fields only)
                if (string.IsNullOrEmpty(timestampCell) && string.IsNullOrEmpty(fullQRCell) && 
                    string.IsNullOrEmpty(kodeItemCell) && string.IsNullOrEmpty(jmlBoxCell))
                {
                    continue;
                }

                // Parse Timestamp
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
                rowData.KodeRak = kodeRakCell; // Just store for reference, no validation needed

                // Validate FullQR
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

                // Validate KodeItem
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

                // Parse JmlBox
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

                // Parse Production Date (optional field)
                if (!string.IsNullOrEmpty(productionDateCell))
                {
                    DateTime productionDate;
                    bool productionDateParsed = false;

                    // Try Excel date format first
                    if (worksheet.Cells[row, 6].Value is double excelDate)
                    {
                        try
                        {
                            productionDate = DateTime.FromOADate(excelDate);
                            rowData.ProductionDate = productionDate;
                            productionDateParsed = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to parse Excel production date {excelDate} at row {row}: {ex.Message}");
                        }
                    }

                    // Try standard DateTime parsing if Excel date failed
                    if (!productionDateParsed)
                    {
                        string[] formats = {
                            "M/d/yy",
                            "M/d/yyyy",
                            "yyyy-MM-dd",
                            "dd/MM/yyyy",
                            "MM/dd/yyyy",
                            "d/M/yyyy",
                            "d/M/yy"
                        };

                        foreach (var format in formats)
                        {
                            if (DateTime.TryParseExact(productionDateCell, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out productionDate))
                            {
                                rowData.ProductionDate = productionDate;
                                productionDateParsed = true;
                                break;
                            }
                        }
                    }

                    if (!productionDateParsed)
                    {
                        rowData.ValidationErrors.Add($"Invalid production date format: {productionDateCell}");
                    }
                }
                // Production Date is optional, so no error if empty

                // Parse Qty Pcs (optional field)
                if (!string.IsNullOrEmpty(qtyPcsCell))
                {
                    if (int.TryParse(qtyPcsCell.Replace(",", "").Replace(".", ""), out int qtyPcs))
                    {
                        if (qtyPcs >= 0)
                        {
                            rowData.QtyPcs = qtyPcs;
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("QtyPcs must be a positive number or zero");
                        }
                    }
                    else
                    {
                        rowData.ValidationErrors.Add($"Invalid QtyPcs format: {qtyPcsCell} (must be a number)");
                    }
                }
                // Qty Pcs is optional, so no error if empty

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

    _logger.LogInformation($"Processed {rows.Count} rows from Excel file");
    return rows;
}

// Updated InsertStorageLogData method - don't save kode_rak
private async Task<int> InsertStorageLogData(List<ExcelRowData> validRows)
{
    var insertedCount = 0;

    try
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            foreach (var row in validRows)
            {
                var storageLog = new StorageLog
                {
                    ItemCode = row.KodeItem,
                    // Skip KodeRak - not stored in database, already included in FullQR
                    FullQR = row.FullQR,
                    StoredAt = row.Timestamp,
                    BoxCount = row.JmlBox,
                    Tanggal = row.Timestamp?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy"),
                    ProductionDate = row.ProductionDate,
                    QtyPcs = row.QtyPcs
                };

                _context.StorageLog.Add(storageLog);
                insertedCount++;

                // Save in batches to avoid memory issues
                if (insertedCount % 100 == 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Batch saved: {insertedCount} records processed");
                }
            }

            // Save remaining records
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation($"Successfully inserted {insertedCount} records into storage_log table");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during database transaction, rolling back");
            throw;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error inserting data into storage_log table");
        throw new Exception($"Database error: {ex.Message}", ex);
    }

    return insertedCount;
}

// Process Excel file for Supply Log
private async Task<List<ExcelRowDataSupply>> ProcessExcelFileSupply(IFormFile file)
{
    var rows = new List<ExcelRowDataSupply>();
    
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

        _logger.LogInformation($"Excel has {rowCount} rows and {colCount} columns");

        if (rowCount <= 1)
        {
            _logger.LogWarning("Excel file has no data rows (only header or empty)");
            return rows;
        }

        // Process data starting from row 2
        // A=item_code, B=full_qr, C=box_count, D=qty_pcs, E=supplied_at, F=to_process
        for (int row = 2; row <= rowCount; row++)
        {
            var rowData = new ExcelRowDataSupply { RowNumber = row };

            try
            {
                var itemCodeCell = worksheet.Cells[row, 1].Value?.ToString()?.Trim(); // A: item_code
                var fullQRCell = worksheet.Cells[row, 2].Value?.ToString()?.Trim();   // B: full_qr
                var boxCountCell = worksheet.Cells[row, 3].Value?.ToString()?.Trim(); // C: box_count
                var qtyPcsCell = worksheet.Cells[row, 4].Value?.ToString()?.Trim();   // D: qty_pcs
                var suppliedAtCell = worksheet.Cells[row, 5].Value?.ToString()?.Trim(); // E: supplied_at
                var toProcessCell = worksheet.Cells[row, 6].Value?.ToString()?.Trim(); // F: to_process

                // Skip completely empty rows
                if (string.IsNullOrEmpty(itemCodeCell) && string.IsNullOrEmpty(fullQRCell) && 
                    string.IsNullOrEmpty(boxCountCell))
                {
                    continue;
                }

                // Validate ItemCode
                if (!string.IsNullOrEmpty(itemCodeCell))
                {
                    if (itemCodeCell.Length > 100)
                    {
                        rowData.ValidationErrors.Add("ItemCode exceeds maximum length (100 characters)");
                    }
                    else
                    {
                        rowData.ItemCode = itemCodeCell;
                    }
                }
                else
                {
                    rowData.ValidationErrors.Add("ItemCode is required");
                }

                // Validate FullQR
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

                // Parse BoxCount (optional)
                if (!string.IsNullOrEmpty(boxCountCell))
                {
                    if (int.TryParse(boxCountCell.Replace(",", "").Replace(".", ""), out int boxCount))
                    {
                        if (boxCount >= 0)
                        {
                            rowData.BoxCount = boxCount;
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("BoxCount must be a positive number or zero");
                        }
                    }
                    else
                    {
                        rowData.ValidationErrors.Add($"Invalid BoxCount format: {boxCountCell} (must be a number)");
                    }
                }

                // Parse QtyPcs (optional)
                if (!string.IsNullOrEmpty(qtyPcsCell))
                {
                    if (int.TryParse(qtyPcsCell.Replace(",", "").Replace(".", ""), out int qtyPcs))
                    {
                        if (qtyPcs >= 0)
                        {
                            rowData.QtyPcs = qtyPcs;
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("QtyPcs must be a positive number or zero");
                        }
                    }
                    else
                    {
                        rowData.ValidationErrors.Add($"Invalid QtyPcs format: {qtyPcsCell} (must be a number)");
                    }
                }

                // Parse SuppliedAt (optional)
                if (!string.IsNullOrEmpty(suppliedAtCell))
                {
                    DateTime suppliedAt;
                    bool suppliedAtParsed = false;

                    // Try Excel date format first
                    if (worksheet.Cells[row, 5].Value is double excelDate)
                    {
                        try
                        {
                            suppliedAt = DateTime.FromOADate(excelDate);
                            rowData.SuppliedAt = suppliedAt;
                            suppliedAtParsed = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to parse Excel supplied date {excelDate} at row {row}: {ex.Message}");
                        }
                    }

                    // Try standard DateTime parsing if Excel date failed
                    if (!suppliedAtParsed)
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
                            if (DateTime.TryParseExact(suppliedAtCell, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out suppliedAt))
                            {
                                rowData.SuppliedAt = suppliedAt;
                                suppliedAtParsed = true;
                                break;
                            }
                        }
                    }

                    if (!suppliedAtParsed)
                    {
                        rowData.ValidationErrors.Add($"Invalid supplied date format: {suppliedAtCell}");
                    }
                }

                // Parse ToProcess (optional)
                if (!string.IsNullOrEmpty(toProcessCell))
                {
                    if (toProcessCell.Length > 100)
                    {
                        rowData.ValidationErrors.Add("ToProcess exceeds maximum length (100 characters)");
                    }
                    else
                    {
                        rowData.ToProcess = toProcessCell;
                    }
                }

                // StorageLogId will be auto-populated based on FullQR matching
                // We'll try to find matching storage_log record by FullQR
                rowData.StorageLogId = null; // Will be populated during database insertion

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
        _logger.LogError(ex, "Error reading Excel file for supply log");
        throw new Exception($"Error reading Excel file: {ex.Message}", ex);
    }

    _logger.LogInformation($"Processed {rows.Count} rows from Excel file for supply log");
    return rows;
}

// Insert Supply Log Data
private async Task<int> InsertSupplyLogData(List<ExcelRowDataSupply> validRows)
{
    var insertedCount = 0;

    try
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            foreach (var row in validRows)
            {
                // Try to find matching storage_log_id based on FullQR
                int? storageLogId = null;
                if (!string.IsNullOrEmpty(row.FullQR))
                {
                    var matchingStorageLog = await _context.StorageLog
                        .Where(sl => sl.FullQR == row.FullQR)
                        .OrderByDescending(sl => sl.LogId) // Get the latest one if multiple matches
                        .FirstOrDefaultAsync();
                    
                    if (matchingStorageLog != null)
                    {
                        storageLogId = matchingStorageLog.LogId;
                        _logger.LogInformation($"Found matching storage_log_id {storageLogId} for FullQR: {row.FullQR}");
                    }
                    else
                    {
                        _logger.LogWarning($"No matching storage_log found for FullQR: {row.FullQR}");
                    }
                }

                var supplyLog = new SupplyLog
                {
                    ItemCode = row.ItemCode,
                    FullQR = row.FullQR,
                    BoxCount = row.BoxCount,
                    QtyPcs = row.QtyPcs,
                    SuppliedAt = row.SuppliedAt,
                    ToProcess = row.ToProcess,
                    Tanggal = row.SuppliedAt?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy"),
                    StorageLogId = storageLogId
                };

                _context.SupplyLog.Add(supplyLog);
                insertedCount++;

                // Save in batches to avoid memory issues
                if (insertedCount % 100 == 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Batch saved: {insertedCount} records processed");
                }
            }

            // Save remaining records
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation($"Successfully inserted {insertedCount} records into supply_log table");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during database transaction, rolling back");
            throw;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error inserting data into supply_log table");
        throw new Exception($"Database error: {ex.Message}", ex);
    }

    return insertedCount;
}

// Process Excel file for Raks
private async Task<List<ExcelRowDataRaks>> ProcessExcelFileRaks(IFormFile file)
{
    var rows = new List<ExcelRowDataRaks>();
    
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

        _logger.LogInformation($"Excel has {rowCount} rows and {colCount} columns");

        if (rowCount <= 1)
        {
            _logger.LogWarning("Excel file has no data rows (only header or empty)");
            return rows;
        }

        // Process data starting from row 2
        // A=full_qr, B=location, C=item_code
        for (int row = 2; row <= rowCount; row++)
        {
            var rowData = new ExcelRowDataRaks { RowNumber = row };

            try
            {
                var fullQRCell = worksheet.Cells[row, 1].Value?.ToString()?.Trim(); // A: full_qr
                var locationCell = worksheet.Cells[row, 2].Value?.ToString()?.Trim(); // B: location
                var itemCodeCell = worksheet.Cells[row, 3].Value?.ToString()?.Trim(); // C: item_code

                // Skip completely empty rows
                if (string.IsNullOrEmpty(fullQRCell) && string.IsNullOrEmpty(locationCell) && 
                    string.IsNullOrEmpty(itemCodeCell))
                {
                    continue;
                }

                // Validate FullQR
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

                // Validate Location
                if (!string.IsNullOrEmpty(locationCell))
                {
                    if (locationCell.Length > 100)
                    {
                        rowData.ValidationErrors.Add("Location exceeds maximum length (100 characters)");
                    }
                    else
                    {
                        rowData.Location = locationCell;
                    }
                }
                else
                {
                    rowData.ValidationErrors.Add("Location is required");
                }

                // Validate ItemCode
                if (!string.IsNullOrEmpty(itemCodeCell))
                {
                    if (itemCodeCell.Length > 100)
                    {
                        rowData.ValidationErrors.Add("ItemCode exceeds maximum length (100 characters)");
                    }
                    else
                    {
                        rowData.ItemCode = itemCodeCell;
                    }
                }
                else
                {
                    rowData.ValidationErrors.Add("ItemCode is required");
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
        _logger.LogError(ex, "Error reading Excel file for raks");
        throw new Exception($"Error reading Excel file: {ex.Message}", ex);
    }

    _logger.LogInformation($"Processed {rows.Count} rows from Excel file for raks");
    return rows;
}

// Insert Raks Data
private async Task<int> InsertRaksData(List<ExcelRowDataRaks> validRows)
{
    var insertedCount = 0;

    try
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            foreach (var row in validRows)
            {
                // Note: You'll need to create a Raks model and add it to your DbContext
                // For now, I'm using a generic approach that you can adapt
                
                // Check if raks table exists and create the record
                // This is a placeholder - you'll need to implement the actual Raks model
                var raksRecord = new
                {
                    FullQR = row.FullQR,
                    Location = row.Location,
                    ItemCode = row.ItemCode
                };

                // Insert using raw SQL since we don't have the Raks model yet
                var sql = "INSERT INTO raks (full_qr, location, item_code) VALUES (@fullQR, @location, @itemCode)";
                var parameters = new[]
                {
                    new Microsoft.Data.SqlClient.SqlParameter("@fullQR", row.FullQR),
                    new Microsoft.Data.SqlClient.SqlParameter("@location", row.Location),
                    new Microsoft.Data.SqlClient.SqlParameter("@itemCode", row.ItemCode)
                };

                await _context.Database.ExecuteSqlRawAsync(sql, parameters);
                insertedCount++;

                // Save in batches to avoid memory issues
                if (insertedCount % 100 == 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Batch saved: {insertedCount} records processed");
                }
            }

            // Save remaining records
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation($"Successfully inserted {insertedCount} records into raks table");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error during database transaction, rolling back");
            throw;
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error inserting data into raks table");
        throw new Exception($"Database error: {ex.Message}", ex);
    }

    return insertedCount;
}

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadTemplate(string uploadType = "storage")
        {
            try
            {
                _logger.LogInformation($"Generating Excel template for upload type: {uploadType}");

                using var package = new OfficeOpenXml.ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Template");

                if (uploadType == "supply")
                {
                    // Supply Log Template - Updated column headers
                    worksheet.Cells[1, 1].Value = "Kode Item";
                    worksheet.Cells[1, 2].Value = "Full QR";
                    worksheet.Cells[1, 3].Value = "Jml box";
                    worksheet.Cells[1, 4].Value = "Qty Pcs";
                    worksheet.Cells[1, 5].Value = "Supplied At";
                    worksheet.Cells[1, 6].Value = "ToProcess";

                    // Add sample data
                    worksheet.Cells[2, 1].Value = "ITEM001";
                    worksheet.Cells[2, 2].Value = "QR123456789";
                    worksheet.Cells[2, 3].Value = 10;
                    worksheet.Cells[2, 4].Value = 100;
                    worksheet.Cells[2, 5].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[2, 6].Value = "PROCESSING";

                    worksheet.Cells[3, 1].Value = "ITEM002";
                    worksheet.Cells[3, 2].Value = "QR987654321";
                    worksheet.Cells[3, 3].Value = 5;
                    worksheet.Cells[3, 4].Value = 50;
                    worksheet.Cells[3, 5].Value = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[3, 6].Value = "SHIPPING";

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 6])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    }

                    // Auto-fit columns
                    worksheet.Cells.AutoFitColumns();

                    var fileName = $"Supply_Log_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileBytes = package.GetAsByteArray();

                    return File(fileBytes, contentType, fileName);
                }
                else if (uploadType == "raks")
                {
                    // Raks Template - Column headers for raks table
                    worksheet.Cells[1, 1].Value = "Full QR";
                    worksheet.Cells[1, 2].Value = "Location";
                    worksheet.Cells[1, 3].Value = "Item Code";

                    // Add sample data
                    worksheet.Cells[2, 1].Value = "QR123456789";
                    worksheet.Cells[2, 2].Value = "RAK-A1-001";
                    worksheet.Cells[2, 3].Value = "ITEM001";

                    worksheet.Cells[3, 1].Value = "QR987654321";
                    worksheet.Cells[3, 2].Value = "RAK-A1-002";
                    worksheet.Cells[3, 3].Value = "ITEM002";

                    worksheet.Cells[4, 1].Value = "QR555666777";
                    worksheet.Cells[4, 2].Value = "RAK-B2-001";
                    worksheet.Cells[4, 3].Value = "ITEM003";

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 3])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                    }

                    // Auto-fit columns
                    worksheet.Cells.AutoFitColumns();

                    var fileName = $"Raks_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileBytes = package.GetAsByteArray();

                    return File(fileBytes, contentType, fileName);
                }
                else
                {
                    // Storage Log Template - Updated column headers
                    worksheet.Cells[1, 1].Value = "Timestamp";
                    worksheet.Cells[1, 2].Value = "Kode Rak";
                    worksheet.Cells[1, 3].Value = "Full QR";
                    worksheet.Cells[1, 4].Value = "Kode Item";
                    worksheet.Cells[1, 5].Value = "Jml box";
                    worksheet.Cells[1, 6].Value = "Production Date";
                    worksheet.Cells[1, 7].Value = "Qty Pcs";

                    // Add sample data
                    worksheet.Cells[2, 1].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[2, 2].Value = "RAK001";
                    worksheet.Cells[2, 3].Value = "QR123456789";
                    worksheet.Cells[2, 4].Value = "ITEM001";
                    worksheet.Cells[2, 5].Value = 10;
                    worksheet.Cells[2, 6].Value = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                    worksheet.Cells[2, 7].Value = 100;

                    worksheet.Cells[3, 1].Value = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[3, 2].Value = "RAK002";
                    worksheet.Cells[3, 3].Value = "QR987654321";
                    worksheet.Cells[3, 4].Value = "ITEM002";
                    worksheet.Cells[3, 5].Value = 5;
                    worksheet.Cells[3, 6].Value = DateTime.Now.AddDays(-25).ToString("yyyy-MM-dd");
                    worksheet.Cells[3, 7].Value = 50;

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 7])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    }

                    // Auto-fit columns
                    worksheet.Cells.AutoFitColumns();

                    var fileName = $"Storage_Log_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                    var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    var fileBytes = package.GetAsByteArray();

                    return File(fileBytes, contentType, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Excel template");
                return BadRequest($"Error generating template: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetDashboardData()
        {
            try
            {
                // Load dari Items table sebagai basis utama
                var allItems = await _context.Items.ToListAsync();
                var allStockSummaries = await _context.StockSummary.ToListAsync();

                // Group stock summaries - sum all records with same item_code
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

                // Combine data
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
                _logger.LogError(ex, "Error getting dashboard data via API (Items-based)");
                return Json(new { error = "Unable to load data", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTableData()
        {
            try
            {
                _logger.LogInformation("Loading table data for DataTables (Items-based)...");

                var allItems = await _context.Items.ToListAsync();
                var allStockSummaries = await _context.StockSummary.ToListAsync();

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
                        RecordCount = g.Count(),
                        Records = g.ToList(), // Keep all records for debugging
                        UniqueFullQRCount = g.Select(r => r.FullQr).Distinct().Count()
                    });

                // Debug logging for TA1240 and TA1400 specifically in GetTableData
                if (stockSummaryGrouped.ContainsKey("TA1240"))
                {
                    var ta1240Data = stockSummaryGrouped["TA1240"];
                    _logger.LogInformation($"GetTableData - TA1240 Debug - Total Records: {ta1240Data.RecordCount}, Unique FullQRs: {ta1240Data.UniqueFullQRCount}, Total Stock: {ta1240Data.TotalCurrentBoxStock}");
                    foreach (var record in ta1240Data.Records)
                    {
                        _logger.LogInformation($"GetTableData - TA1240 Record - FullQR: {record.FullQr}, CurrentBoxStock: {record.CurrentBoxStock}, LastUpdated: {record.LastUpdated}");
                    }
                }
                
                if (stockSummaryGrouped.ContainsKey("TA1400"))
                {
                    var ta1400Data = stockSummaryGrouped["TA1400"];
                    _logger.LogInformation($"GetTableData - TA1400 Debug - Total Records: {ta1400Data.RecordCount}, Unique FullQRs: {ta1400Data.UniqueFullQRCount}, Total Stock: {ta1400Data.TotalCurrentBoxStock}");
                    foreach (var record in ta1400Data.Records)
                    {
                        _logger.LogInformation($"GetTableData - TA1400 Record - LogId: {record.LogId}, FullQR: {record.FullQr}, CurrentBoxStock: {record.CurrentBoxStock}, LastUpdated: {record.LastUpdated}");
                    }
                }

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

                // Debug logging untuk shortage items
                var shortageItems = tableData.Where(x => x.Status == "Shortage").ToList();
                _logger.LogInformation($"GetTableData - Shortage Items Count: {shortageItems.Count}");
                
                // Log beberapa contoh shortage items
                foreach (var item in shortageItems.Take(5))
                {
                    _logger.LogInformation($"GetTableData - Shortage Item: {item.ItemCode}, Stock: {item.CurrentBoxStock}, Min: {item.StandardMin}");
                }

                _logger.LogInformation($"Prepared {tableData.Count} records for table display (Items-based)");

                return Json(new { data = tableData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading table data (Items-based)");
                return Json(new { error = "Unable to load table data", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetItemsWithStockComparison()
        {
            try
            {
                var allItems = await _context.Items.ToListAsync();
                var allStockSummaries = await _context.StockSummary.ToListAsync();

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
                    TotalItemsInItemsTable = allItems.Count,
                    TotalRecordsInStockView = allStockSummaries.Count,
                    UniqueItemCodesInStockView = stockSummaryGrouped.Count,
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
                        .Select(kvp => new { 
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

        [HttpGet]
        public async Task<JsonResult> GetDataConsistency()
        {
            try
            {
                // Load data seperti di Index method
                var allItems = await _context.Items.ToListAsync();
                var allStockSummaries = await _context.StockSummary.ToListAsync();

                var stockSummaryGrouped = allStockSummaries
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0),
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
                                 : DateTime.MinValue
                }).ToList();

                // Hitung stats seperti di Index
                var expiredCount = itemsWithStockData.Count(x => x.TotalCurrentBoxStock > 0 && IsExpired(x.LastUpdated, x.Item.StandardExp));
                var nearExpiredCount = itemsWithStockData.Count(x => x.TotalCurrentBoxStock > 0 && IsNearExpired(x.LastUpdated, x.Item.StandardExp));
                var shortageCount = itemsWithStockData.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin));
                var aboveMaxCount = itemsWithStockData.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax));

                // Hitung status seperti di tabel
                var shortageItems = itemsWithStockData.Where(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)).ToList();
                var shortageItemsWithStatus = shortageItems.Select(x => new
                {
                    ItemCode = x.Item.ItemCode,
                    Stock = x.TotalCurrentBoxStock,
                    Min = x.Item.StandardMin,
                    Status = DetermineItemStatus(x.TotalCurrentBoxStock, x.LastUpdated, x.Item)
                }).ToList();

                return Json(new
                {
                    success = true,
                    totalItems = allItems.Count,
                    statsShortageCount = shortageCount,
                    tableShortageCount = shortageItemsWithStatus.Count(x => x.Status == "Shortage"),
                    shortageItems = shortageItemsWithStatus.Take(10).ToList(),
                    expiredCount = expiredCount,
                    nearExpiredCount = nearExpiredCount,
                    aboveMaxCount = aboveMaxCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // IMPROVED: Helper method untuk menentukan status item dengan logika yang benar
        // Now uses status_expired from database when available
        private string DetermineItemStatus(int currentBoxStock, DateTime lastUpdated, Item item, string? statusExpired = null)
{
    // If database provides status_expired, use it for expiry-related status
    if (!string.IsNullOrEmpty(statusExpired))
    {
        // Map database status to our status strings
        // Database values: "tidak ada stok", "expired", "hampir expired", "normal", etc.
        
        // Check for expired status
        if (statusExpired.Equals("expired", StringComparison.OrdinalIgnoreCase))
        {
            return "Already Expired";
        }
        
        // Check for near expired status
        if (statusExpired.Contains("hampir", StringComparison.OrdinalIgnoreCase))
        {
            return "Near Expired";
        }

        // Check for ok status
        if (statusExpired.Equals("ok", StringComparison.OrdinalIgnoreCase) ||
            statusExpired.Equals("normal", StringComparison.OrdinalIgnoreCase))
        {
            return "Normal";
        }
        
        // "tidak ada stok" means no stock, which should be handled by shortage logic below
        // So we continue to manual calculation for stock-level status
    }

    // Fallback to manual calculation if database status not available or for stock-level status
    // Prioritas 1: Check for expired (hanya jika stok > 0)
    if (currentBoxStock > 0 && IsExpired(lastUpdated, item.StandardExp))
        return "Already Expired";

    // Prioritas 2: Check for near expired (hanya jika stok > 0)
    if (currentBoxStock > 0 && IsNearExpired(lastUpdated, item.StandardExp))
        return "Near Expired";

    // Prioritas 3: Check stock levels (termasuk stok = 0)
    if (IsShortage(currentBoxStock, item.StandardMin))
        return "Shortage";

    if (IsAboveMax(currentBoxStock, item.StandardMax))
        return "Over Stock";

    return "Normal";
}


        // IMPROVED: Helper methods dengan logika yang lebih jelas
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
            return daysUntilExpiry < 0; // Already expired
        }

        private bool IsNearExpired(DateTime lastUpdated, int? standardExp)
        {
            if (!standardExp.HasValue || lastUpdated == DateTime.MinValue) return false;
            
            var daysUntilExpiry = CalculateDaysUntilExpiry(lastUpdated, standardExp);
            var nearExpiredThreshold = GetNearExpiredThreshold(standardExp.Value);
            return daysUntilExpiry >= 0 && daysUntilExpiry <= nearExpiredThreshold;
        }

        // NEW: Method untuk shortage (0 <= currentStock <= standardMin)
        private bool IsShortage(int currentStock, int? standardMin)
        {
            if (!standardMin.HasValue) return false;
            return currentStock >= 0 && currentStock <= standardMin.Value;
        }

        // UPDATED: Method untuk below min sudah tidak digunakan karena digantikan shortage
        private bool IsBelowMin(int totalStock, int? standardMin)
        {
            // Ini sekarang tidak digunakan karena sudah digantikan oleh IsShortage
            // Tapi tetap ada untuk backward compatibility
            return false; // Always return false since we use Shortage now
        }

        private bool IsAboveMax(int totalStock, int? standardMax)
        {
            return standardMax.HasValue && totalStock > standardMax.Value;
        }

        // Helper method to get near expired threshold based on standard exp
        private int GetNearExpiredThreshold(int standardExp)
        {
            // Jika standard exp <= 3 hari, maka nearly expired = 1 hari
            // Jika standard exp > 3 hari, maka nearly expired = 3 hari
            return standardExp <= 3 ? 1 : 3;
        }

        // Green Hose Input Page
        public async Task<IActionResult> GreenHoseInput()
        {
            ViewData["Title"] = "Green Hose Input";
            return View();
        }

        // Get FIFO Recommendations for Green Hose
        [HttpGet]
        public async Task<JsonResult> GetFIFORecommendations(string searchTerm = "")
        {
            try
            {
                // Get items from storage_log with oldest production date (FIFO - First In First Out)
                // Group by item_code and full_qr to get current stock
                var recommendations = await _context.StorageLog
                    .Where(s => s.ProductionDate.HasValue && s.BoxCount > 0)
                    .Where(s => string.IsNullOrEmpty(searchTerm) || s.ItemCode.Contains(searchTerm))
                    .GroupBy(s => new { s.ItemCode, s.FullQR })
                    .Select(g => new
                    {
                        itemCode = g.Key.ItemCode,
                        fullQr = g.Key.FullQR,
                        productionDate = g.OrderBy(x => x.ProductionDate).First().ProductionDate,
                        boxCount = g.Sum(x => x.BoxCount),
                        qtyPcs = g.Sum(x => x.QtyPcs ?? 0),
                        daysOld = g.OrderBy(x => x.ProductionDate).First().ProductionDate.HasValue 
                            ? (DateTime.Now - g.OrderBy(x => x.ProductionDate).First().ProductionDate.Value).Days 
                            : 0
                    })
                    .OrderBy(s => s.productionDate) // Oldest first (FIFO)
                    .Take(10)
                    .ToListAsync();

                return Json(new { success = true, data = recommendations });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting FIFO recommendations");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Submit Green Hose Input (IN/OUT)
        [HttpPost]
        public async Task<JsonResult> SubmitGreenHoseInput([FromBody] GreenHoseInputModel model)
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

                if (model.TransactionType == "IN")
                {
                    // INPUT IN - Add to storage_log
                    var storageLog = new StorageLog
                    {
                        ItemCode = model.ItemCode,
                        FullQR = model.FullQR,
                        StoredAt = DateTime.Now,
                        BoxCount = model.BoxCount ?? 0,
                        Tanggal = DateTime.Now.ToString("dd/MM/yyyy"),
                        ProductionDate = model.ProductionDate,
                        QtyPcs = model.QtyPcs
                    };

                    _context.StorageLog.Add(storageLog);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Data successfully saved to storage" });
                }
                else if (model.TransactionType == "OUT")
                {
                    // INPUT OUT - Add to supply_log
                    var supplyLog = new SupplyLog
                    {
                        ItemCode = model.ItemCode,
                        FullQR = model.FullQR,
                        SuppliedAt = DateTime.Now,
                        BoxCount = model.BoxCount ?? 0,
                        Tanggal = DateTime.Now.ToString("dd/MM/yyyy"),
                        // ProductionDate = model.ProductionDate, // Property not available in SupplyLog yet
                        QtyPcs = model.QtyPcs,
                        ToProcess = model.ToProcess ?? "Production"
                    };

                    _context.SupplyLog.Add(supplyLog);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Data successfully saved to supply log" });
                }
                else
                {
                    return Json(new { success = false, message = "Invalid transaction type" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting Green Hose input");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // Get Item Codes for Select2 Autocomplete
        [HttpGet]
        public async Task<JsonResult> GetItemCodes(string search = "")
        {
            try
            {
                var query = _context.Items.AsQueryable();

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

        // Get Full QR Codes for Select2 Autocomplete
        [HttpGet]
        public async Task<JsonResult> GetFullQRCodes(string search = "")
        {
            try
            {
                var query = _context.Set<Rak>().AsQueryable();

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


        // Existing methods...
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [Authorize]
        public IActionResult History()
        {
            return View("GreenHoseHistory");
        }

        [HttpGet]
        public async Task<JsonResult> GetHistoryData(string type, DateTime? date = null)
        {
            try
            {
                if (type == "in")
                {
                    var query = _context.StorageLog.AsQueryable();
                    
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
                    var query = _context.SupplyLog.AsQueryable();

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
                            qty = x.BoxCount ?? 0,
                            qtyPcs = x.QtyPcs ?? 0,
                            status = "OUT",
                            id = x.LogId
                        })
                        .ToListAsync();
                    return Json(new { success = true, data });
                }
                else if (type == "stock")
                {
                    var allStock = await _context.StockSummary.ToListAsync();
                    var allItems = await _context.Items.ToDictionaryAsync(i => i.ItemCode); // Fetch items for Pcs calc
                    
                    // Group similar to GetTableData logic
                    var grouped = allStock
                        .GroupBy(x => x.ItemCode)
                        .Select(g => {
                            // Logic untuk ambil status terbaru
                            var latestRecord = g.Where(s => s.ParsedLastUpdated.HasValue)
                                               .OrderByDescending(s => s.ParsedLastUpdated)
                                               .FirstOrDefault();
                            
                            var totalStock = g.Sum(x => x.CurrentBoxStock ?? 0);
                            var statusExpired = latestRecord?.StatusExpired;
                            var lastUpdated = latestRecord?.ParsedLastUpdated ?? DateTime.MinValue;
                            
                            // Get Item info from dictionary based on ItemCode
                            var item = allItems.ContainsKey(g.Key) ? allItems[g.Key] : null;
                            
                            // Calculate Pcs
                            var qtyPerBox = item?.QtyPerBox ?? 0;
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
                                // If no item data but we have database status, use basic mapping
                                if (statusExpired.Equals("expired", StringComparison.OrdinalIgnoreCase)) displayStatus = "Already Expired";
                                else if (statusExpired.Contains("hampir", StringComparison.OrdinalIgnoreCase)) displayStatus = "Near Expired";
                                else if (statusExpired.Equals("ok", StringComparison.OrdinalIgnoreCase)) displayStatus = "Normal";
                                else displayStatus = statusExpired; 
                            }

                            return new {
                                itemCode = g.Key,
                                description = displayStatus,
                                qty = totalStock,
                                qtyPcs = totalPcs, // Added Pcs
                                lastUpdated = lastUpdated != DateTime.MinValue ? lastUpdated.ToString("dd-MM-yyyy HH:mm") : "-" // Added LastUpdated
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
                    var log = await _context.StorageLog.FindAsync(id);
                    if (log == null) return Json(new { success = false, message = "Record not found" });
                    _context.StorageLog.Remove(log);
                }
                else if (type == "out")
                {
                    var log = await _context.SupplyLog.FindAsync(id);
                    if (log == null) return Json(new { success = false, message = "Record not found" });
                    _context.SupplyLog.Remove(log);
                }
                else
                {
                    return Json(new { success = false, message = "Invalid type" });
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
    }
}