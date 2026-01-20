    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using dashboardWIPHouse.Data;
    using dashboardWIPHouse.Models;
    using System.Diagnostics;
    using System.Globalization;
    using OfficeOpenXml;

    namespace dashboardWIPHouse.Controllers
    {
        public class RVIController : Controller
        {
            private readonly ILogger<RVIController> _logger;
            private readonly RVIContext _context;

            public RVIController(ILogger<RVIController> logger, RVIContext context)
            {
                _logger = logger;
                _context = context;
            }

            // RVI Login is now handled by AccountController

            // GET: RVI Dashboard
            public async Task<IActionResult> Index()
            {
                // Check if user is logged in and has RVI database claim
                _logger.LogInformation($"RVI Index - User authenticated: {User.Identity.IsAuthenticated}");
                _logger.LogInformation($"RVI Index - User name: {User.Identity.Name}");
                _logger.LogInformation($"RVI Index - Database claim: {User.FindFirst("Database")?.Value}");
                
                if (!User.Identity.IsAuthenticated || User.FindFirst("Database")?.Value != "RVI")
                {
                    _logger.LogWarning("RVI Index - Authentication failed, redirecting to login");
                    return RedirectToAction("Login", "Account");
                }

                try
                {
                    _logger.LogInformation("=== RVI Dashboard Load Started ===");

                    // Test database connection
                    _logger.LogInformation("Testing RVI database connection...");
                    var canConnect = await _context.Database.CanConnectAsync();
                    _logger.LogInformation($"RVI Database connection result: {canConnect}");

                    if (!canConnect)
                    {
                        throw new Exception("Cannot connect to RVI database");
                    }

                // Load data from ItemsRVI table and StockSummaryRVI view
                _logger.LogInformation("Loading RVI Items data...");
                var allItems = await _context.ItemsRVI.ToListAsync();
                _logger.LogInformation($"Loaded {allItems.Count} items from RVI items table");

                _logger.LogInformation("Loading RVI Stock Summary data...");
                var allStockSummaries = await _context.StockSummaryRVI.ToListAsync();
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from RVI vw_stock_summary");

                // Group stock summaries by item_code untuk aggregation
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

                // Combine Items dengan Stock Summary data
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

                _logger.LogInformation($"Combined data for {itemsWithStockData.Count} RVI items");
                _logger.LogInformation($"Items with stock data: {itemsWithStockData.Count(x => x.HasStockData)}");
                _logger.LogInformation($"Items without stock data: {itemsWithStockData.Count(x => !x.HasStockData)}");

                // Filter items berdasarkan kriteria tertentu (optional)
                var filteredItems = itemsWithStockData.ToList();

                // Calculate dashboard summary for RVI (no expiry tracking)
                var dashboardSummary = new DashboardSummaryRVI
                {
                    TotalItems = filteredItems.Count,
                    ShortageCount = filteredItems.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    NormalCount = filteredItems.Count(x => IsNormal(x.TotalCurrentBoxStock, x.Item.StandardMin, x.Item.StandardMax)),
                    OverStockCount = filteredItems.Count(x => IsOverStock(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                // Detailed logging for verification
                _logger.LogInformation($"RVI Dashboard Summary:");
                _logger.LogInformation($"- Total Items: {dashboardSummary.TotalItems}");
                _logger.LogInformation($"- Shortage: {dashboardSummary.ShortageCount}");
                _logger.LogInformation($"- Normal: {dashboardSummary.NormalCount}");
                _logger.LogInformation($"- Over Stock: {dashboardSummary.OverStockCount}");

                // Log beberapa contoh items untuk debugging
                var sampleItems = filteredItems.Take(5).ToList();
                foreach (var item in sampleItems)
                {
                    _logger.LogInformation($"Sample - {item.Item.ItemCode}: Stock={item.TotalCurrentBoxStock}, " +
                        $"Min={item.Item.StandardMin}, Max={item.Item.StandardMax}, Status={DetermineItemStatus(item.TotalCurrentBoxStock, item.LastUpdated, item.Item)}");
                }

                    ViewData["Title"] = "RVI Dashboard";
                    _logger.LogInformation("=== RVI Dashboard Load Completed Successfully ===");
                    return View(dashboardSummary);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"=== RVI Dashboard Load Failed ===");
                    _logger.LogError($"Error: {ex.Message}");
                    _logger.LogError($"Stack Trace: {ex.StackTrace}");

                    var emptySummary = new DashboardSummaryRVI();
                    ViewData["Title"] = "RVI Dashboard";
                    ViewData["Error"] = $"Unable to load RVI dashboard data: {ex.Message}";
                    return View(emptySummary);
                }
            }

            [HttpGet]
            public async Task<JsonResult> GetDashboardData()
            {
                try
                {
                    // Load dari ItemsRVI table dan StockSummaryRVI view
                    var allItems = await _context.ItemsRVI.ToListAsync();
                    var allStockSummaries = await _context.StockSummaryRVI.ToListAsync();

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

                    var dashboardSummary = new DashboardSummaryRVI
                    {
                        TotalItems = filteredItems.Count,
                        ShortageCount = filteredItems.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                        NormalCount = filteredItems.Count(x => IsNormal(x.TotalCurrentBoxStock, x.Item.StandardMin, x.Item.StandardMax)),
                        OverStockCount = filteredItems.Count(x => IsOverStock(x.TotalCurrentBoxStock, x.Item.StandardMax))
                    };

                    return Json(dashboardSummary);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting RVI dashboard data via API");
                    return Json(new { error = "Unable to load data", details = ex.Message });
                }
            }

            [HttpGet]
            public async Task<JsonResult> GetTableData()
            {
                try
                {
                    _logger.LogInformation("Loading RVI table data for DataTables...");

                    var allItems = await _context.ItemsRVI.ToListAsync();
                    var allStockSummaries = await _context.StockSummaryRVI.ToListAsync();

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
                            QtyPerBox = item.QtyPerBox,
                            HasStockData = hasStockData,
                            StockRecordCount = stockData?.RecordCount ?? 0
                        };
                    })
                    .OrderBy(x => x.ItemCode)
                    .ToList();

                    _logger.LogInformation($"Prepared {tableData.Count} RVI records for table display");

                    return Json(new { data = tableData });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading RVI table data");
                    return Json(new { error = "Unable to load table data", details = ex.Message });
                }
            }

            [HttpPost]
            public async Task<JsonResult> UploadExcel(IFormFile file, string uploadType = "storage")
            {
                var result = new ExcelUploadResultRVI();

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

                    _logger.LogInformation($"Processing RVI Excel file: {file.FileName}, Size: {file.Length} bytes, Upload Type: {uploadType}");

                    int insertedCount = 0;

                    if (uploadType == "storage")
                    {
                        // Process for RVI Storage Log
                        var excelDataStorage = await ProcessExcelFileRVIStorage(file);
                        
                        if (!excelDataStorage.Any())
                        {
                            result.Message = "No valid data found in Excel file";
                            return Json(result);
                        }

                        // Validate all rows
                        var validRowsStorage = new List<ExcelRowDataRVIStorage>();
                        foreach (var row in excelDataStorage)
                        {
                            if (row.IsValid)
                            {
                                validRowsStorage.Add(row);
                            }
                            else
                            {
                                result.DetailedErrors.Add(new ExcelRowErrorRVI
                                {
                                    RowNumber = row.RowNumber,
                                    Error = string.Join(", ", row.ValidationErrors),
                                    RowData = $"Timestamp: {row.Timestamp}, FullQR: {row.FullQR}, KodeItem: {row.KodeItem}, JmlBox: {row.JmlBox}, ProductionDate: {row.ProductionDate}, QtyPcs: {row.QtyPcs}"
                                });
                            }
                        }

                        result.ProcessedRows = excelDataStorage.Count;
                        result.ErrorRows = result.DetailedErrors.Count;

                        if (!validRowsStorage.Any())
                        {
                            result.Message = "No valid rows found to import";
                            result.Errors = result.DetailedErrors.Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                            return Json(result);
                        }

                        // Insert valid data to database
                        insertedCount = await InsertRVIStorageLogData(validRowsStorage);
                    }
                    else if (uploadType == "supply")
                    {
                        // Process for RVI Supply Log
                        var excelDataSupply = await ProcessExcelFileRVISupply(file);
                        
                        if (!excelDataSupply.Any())
                        {
                            result.Message = "No valid data found in Excel file";
                            return Json(result);
                        }

                        // Validate all rows
                        var validRowsSupply = new List<ExcelRowDataRVISupply>();
                        foreach (var row in excelDataSupply)
                        {
                            if (row.IsValid)
                            {
                                validRowsSupply.Add(row);
                            }
                            else
                            {
                                result.DetailedErrors.Add(new ExcelRowErrorRVI
                                {
                                    RowNumber = row.RowNumber,
                                    Error = string.Join(", ", row.ValidationErrors),
                                    RowData = $"Timestamp: {row.Timestamp}, KodeItem: {row.KodeItem}, JmlBox: {row.JmlBox}, ProductionDate: {row.ProductionDate}, QtyPcs: {row.QtyPcs}"
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
                        insertedCount = await InsertRVISupplyLogData(validRowsSupply);
                    }
                    else
                    {
                        // Process for RVI Items (existing logic)
                        var excelData = await ProcessExcelFileRVI(file);
                        
                        if (!excelData.Any())
                        {
                            result.Message = "No valid data found in Excel file";
                            return Json(result);
                        }

                        // Validate all rows
                        var validRows = new List<ExcelRowDataRVI>();
                        foreach (var row in excelData)
                        {
                            if (row.IsValid)
                            {
                                validRows.Add(row);
                            }
                            else
                            {
                                result.DetailedErrors.Add(new ExcelRowErrorRVI
                                {
                                    RowNumber = row.RowNumber,
                                    Error = string.Join(", ", row.ValidationErrors),
                                    RowData = $"ItemCode: {row.ItemCode}, QtyPerBox: {row.QtyPerBox}, StandardMin: {row.StandardMin}, StandardMax: {row.StandardMax}"
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
                        insertedCount = await InsertRVIItemsData(validRows);
                    }
                    
                    result.Success = true;
                    result.SuccessfulRows = insertedCount;
                    result.Message = $"Successfully imported {insertedCount} records from {result.ProcessedRows} total rows";
                    
                    if (result.ErrorRows > 0)
                    {
                        result.Message += $" ({result.ErrorRows} rows had errors and were skipped)";
                        result.Errors = result.DetailedErrors.Take(10).Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                    }

                    _logger.LogInformation($"RVI Excel upload completed: {insertedCount} successful, {result.ErrorRows} errors");

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing RVI Excel upload");
                    result.Message = $"Error processing file: {ex.Message}";
                    result.Errors.Add(ex.Message);
                }

                return Json(result);
            }

            // Process Excel file for RVI Storage Log
            private async Task<List<ExcelRowDataRVIStorage>> ProcessExcelFileRVIStorage(IFormFile file)
            {
                var rows = new List<ExcelRowDataRVIStorage>();
                
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

                    _logger.LogInformation($"RVI Storage Excel has {rowCount} rows and {colCount} columns");

                    if (rowCount <= 1)
                    {
                        _logger.LogWarning("RVI Storage Excel file has no data rows (only header or empty)");
                        return rows;
                    }

                    // Process data starting from row 2
                    // A=Timestamp, B=Kode Rak (skip), C=Full QR, D=Kode Item, E=Jml Box, F=Production Date, G=Qty Pcs
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowData = new ExcelRowDataRVIStorage { RowNumber = row };

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
                            _logger.LogError(ex, $"Error processing RVI storage row {row}");
                            rowData.ValidationErrors.Add($"Error processing row: {ex.Message}");
                            rows.Add(rowData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading RVI storage Excel file");
                    throw new Exception($"Error reading Excel file: {ex.Message}", ex);
                }

                _logger.LogInformation($"Processed {rows.Count} rows from RVI storage Excel file");
                return rows;
            }

            private async Task<List<ExcelRowDataRVI>> ProcessExcelFileRVI(IFormFile file)
            {
                var rows = new List<ExcelRowDataRVI>();
                
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

                    _logger.LogInformation($"RVI Excel has {rowCount} rows and {colCount} columns");

                    if (rowCount <= 1)
                    {
                        _logger.LogWarning("RVI Excel file has no data rows (only header or empty)");
                        return rows;
                    }

                    // Process data starting from row 2
                    // A=ItemCode, B=QtyPerBox, C=StandardMin, D=StandardMax
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowData = new ExcelRowDataRVI { RowNumber = row };

                        try
                        {
                            var itemCodeCell = worksheet.Cells[row, 1].Value?.ToString()?.Trim(); // A: ItemCode
                            var qtyPerBoxCell = worksheet.Cells[row, 2].Value?.ToString()?.Trim(); // B: QtyPerBox
                            var standardMinCell = worksheet.Cells[row, 3].Value?.ToString()?.Trim(); // C: StandardMin
                            var standardMaxCell = worksheet.Cells[row, 4].Value?.ToString()?.Trim(); // D: StandardMax

                            // Skip completely empty rows
                            if (string.IsNullOrEmpty(itemCodeCell) && string.IsNullOrEmpty(qtyPerBoxCell) && 
                                string.IsNullOrEmpty(standardMinCell) && string.IsNullOrEmpty(standardMaxCell))
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

                            // Parse QtyPerBox
                            if (!string.IsNullOrEmpty(qtyPerBoxCell))
                            {
                                if (decimal.TryParse(qtyPerBoxCell.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal qtyPerBox))
                                {
                                    if (qtyPerBox >= 0)
                                    {
                                        rowData.QtyPerBox = qtyPerBox;
                                    }
                                    else
                                    {
                                        rowData.ValidationErrors.Add("QtyPerBox must be a positive number or zero");
                                    }
                                }
                                else
                                {
                                    rowData.ValidationErrors.Add($"Invalid QtyPerBox format: {qtyPerBoxCell} (must be a number)");
                                }
                            }
                            else
                            {
                                rowData.ValidationErrors.Add("QtyPerBox is required");
                            }

                            // Parse StandardMin
                            if (!string.IsNullOrEmpty(standardMinCell))
                            {
                                if (int.TryParse(standardMinCell.Replace(",", "").Replace(".", ""), out int standardMin))
                                {
                                    if (standardMin >= 0)
                                    {
                                        rowData.StandardMin = standardMin;
                                    }
                                    else
                                    {
                                        rowData.ValidationErrors.Add("StandardMin must be a positive number or zero");
                                    }
                                }
                                else
                                {
                                    rowData.ValidationErrors.Add($"Invalid StandardMin format: {standardMinCell} (must be a number)");
                                }
                            }
                            else
                            {
                                rowData.ValidationErrors.Add("StandardMin is required");
                            }

                            // Parse StandardMax
                            if (!string.IsNullOrEmpty(standardMaxCell))
                            {
                                if (int.TryParse(standardMaxCell.Replace(",", "").Replace(".", ""), out int standardMax))
                                {
                                    if (standardMax >= 0)
                                    {
                                        rowData.StandardMax = standardMax;
                                    }
                                    else
                                    {
                                        rowData.ValidationErrors.Add("StandardMax must be a positive number or zero");
                                    }
                                }
                                else
                                {
                                    rowData.ValidationErrors.Add($"Invalid StandardMax format: {standardMaxCell} (must be a number)");
                                }
                            }
                            else
                            {
                                rowData.ValidationErrors.Add("StandardMax is required");
                            }

                            // Validate StandardMax > StandardMin
                            if (rowData.StandardMin.HasValue && rowData.StandardMax.HasValue && 
                                rowData.StandardMax.Value <= rowData.StandardMin.Value)
                            {
                                rowData.ValidationErrors.Add("StandardMax must be greater than StandardMin");
                            }

                            rows.Add(rowData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing RVI row {row}");
                            rowData.ValidationErrors.Add($"Error processing row: {ex.Message}");
                            rows.Add(rowData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading RVI Excel file");
                    throw new Exception($"Error reading Excel file: {ex.Message}", ex);
                }

                _logger.LogInformation($"Processed {rows.Count} rows from RVI Excel file");
                return rows;
            }

            private async Task<int> InsertRVIItemsData(List<ExcelRowDataRVI> validRows)
            {
                var insertedCount = 0;

                try
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    
                    try
                    {
                        foreach (var row in validRows)
                        {
                            // Check if item already exists
                            var existingItem = await _context.ItemsRVI.FindAsync(row.ItemCode);
                            
                            if (existingItem != null)
                            {
                                // Update existing item
                                existingItem.QtyPerBox = row.QtyPerBox;
                                existingItem.StandardMin = row.StandardMin;
                                existingItem.StandardMax = row.StandardMax;
                                _context.ItemsRVI.Update(existingItem);
                            }
                            else
                            {
                                // Create new item
                                var newItem = new ItemRVI
                                {
                                    ItemCode = row.ItemCode,
                                    QtyPerBox = row.QtyPerBox,
                                    StandardMin = row.StandardMin,
                                    StandardMax = row.StandardMax
                                };
                                _context.ItemsRVI.Add(newItem);
                            }
                            
                            insertedCount++;

                            // Save in batches to avoid memory issues
                            if (insertedCount % 100 == 0)
                            {
                                await _context.SaveChangesAsync();
                                _logger.LogInformation($"RVI Batch saved: {insertedCount} records processed");
                            }
                        }

                        // Save remaining records
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        
                        _logger.LogInformation($"Successfully inserted/updated {insertedCount} records into RVI items table");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error during RVI database transaction, rolling back");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting data into RVI items table");
                    throw new Exception($"Database error: {ex.Message}", ex);
                }

                return insertedCount;
            }

            // Process Excel file for RVI Supply Log
            private async Task<List<ExcelRowDataRVISupply>> ProcessExcelFileRVISupply(IFormFile file)
            {
                var rows = new List<ExcelRowDataRVISupply>();
                
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

                    _logger.LogInformation($"RVI Supply Excel has {rowCount} rows and {colCount} columns");

                    if (rowCount <= 1)
                    {
                        _logger.LogWarning("RVI Supply Excel file has no data rows (only header or empty)");
                        return rows;
                    }

                    // Process data starting from row 2
                    // A=Timestamp, B=Kode Item, C=Jml Box, D=Production Date, E=Qty Pcs
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowData = new ExcelRowDataRVISupply { RowNumber = row };

                        try
                        {
                            var timestampCell = worksheet.Cells[row, 1].Value?.ToString()?.Trim(); // A: Timestamp
                            var kodeItemCell = worksheet.Cells[row, 2].Value?.ToString()?.Trim();   // B: Kode Item
                            var jmlBoxCell = worksheet.Cells[row, 3].Value?.ToString()?.Trim();     // C: Jml Box
                            var productionDateCell = worksheet.Cells[row, 4].Value?.ToString()?.Trim(); // D: Production Date
                            var qtyPcsCell = worksheet.Cells[row, 5].Value?.ToString()?.Trim();     // E: Qty Pcs

                            // Skip completely empty rows
                            if (string.IsNullOrEmpty(timestampCell) && string.IsNullOrEmpty(kodeItemCell) && 
                                string.IsNullOrEmpty(jmlBoxCell))
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
                                if (worksheet.Cells[row, 4].Value is double excelDate)
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

                            rows.Add(rowData);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Error processing RVI supply row {row}");
                            rowData.ValidationErrors.Add($"Error processing row: {ex.Message}");
                            rows.Add(rowData);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading RVI supply Excel file");
                    throw new Exception($"Error reading Excel file: {ex.Message}", ex);
                }

                _logger.LogInformation($"Processed {rows.Count} rows from RVI supply Excel file");
                return rows;
            }

            // Insert RVI Storage Log Data
            private async Task<int> InsertRVIStorageLogData(List<ExcelRowDataRVIStorage> validRows)
            {
                var insertedCount = 0;

                try
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    
                    try
                    {
                        foreach (var row in validRows)
                        {
                            var storageLog = new StorageLogRVI
                            {
                                ItemCode = row.KodeItem,
                                // Skip KodeRak - not stored in database, already included in FullQR
                                FullQR = row.FullQR,
                                StoredAt = row.Timestamp ?? DateTime.Now,
                                BoxCount = row.JmlBox,
                                Tanggal = row.Timestamp?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy"),
                                ProductionDate = row.ProductionDate,
                                QtyPcs = row.QtyPcs
                            };

                            _context.StorageLogRVI.Add(storageLog);
                            insertedCount++;

                            // Save in batches to avoid memory issues
                            if (insertedCount % 100 == 0)
                            {
                                await _context.SaveChangesAsync();
                                _logger.LogInformation($"RVI Storage Batch saved: {insertedCount} records processed");
                            }
                        }

                        // Save remaining records
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        
                        _logger.LogInformation($"Successfully inserted {insertedCount} records into RVI storage_log table");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error during RVI storage database transaction, rolling back");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting data into RVI storage_log table");
                    throw new Exception($"Database error: {ex.Message}", ex);
                }

                return insertedCount;
            }

            // Insert RVI Supply Log Data
            private async Task<int> InsertRVISupplyLogData(List<ExcelRowDataRVISupply> validRows)
            {
                var insertedCount = 0;

                try
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    
                    try
                    {
                        foreach (var row in validRows)
                        {
                            var supplyLog = new SupplyLogRVI
                            {
                                ItemCode = row.KodeItem,
                                SuppliedAt = row.Timestamp ?? DateTime.Now,
                                BoxCount = row.JmlBox,
                                Tanggal = row.Timestamp?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy"),
                                ProductionDate = row.ProductionDate,
                                QtyPcs = row.QtyPcs
                            };

                            _context.SupplyLogRVI.Add(supplyLog);
                            insertedCount++;

                            // Save in batches to avoid memory issues
                            if (insertedCount % 100 == 0)
                            {
                                await _context.SaveChangesAsync();
                                _logger.LogInformation($"RVI Supply Batch saved: {insertedCount} records processed");
                            }
                        }

                        // Save remaining records
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        
                        _logger.LogInformation($"Successfully inserted {insertedCount} records into RVI supply_log table");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error during RVI supply database transaction, rolling back");
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inserting data into RVI supply_log table");
                    throw new Exception($"Database error: {ex.Message}", ex);
                }

                return insertedCount;
            }

            [HttpGet]
            public IActionResult DownloadTemplate(string uploadType = "storage")
            {
                try
                {
                    _logger.LogInformation($"Generating RVI Excel template for upload type: {uploadType}");

                    using var package = new OfficeOpenXml.ExcelPackage();
                    var worksheet = package.Workbook.Worksheets.Add("Template");

                    if (uploadType == "storage")
                    {
                        // RVI Storage Log Template - Updated column headers
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

                        var fileName = $"RVI_Storage_Log_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        var fileBytes = package.GetAsByteArray();

                        return File(fileBytes, contentType, fileName);
                    }
                    else if (uploadType == "supply")
                    {
                        // RVI Supply Log Template - Column headers
                        worksheet.Cells[1, 1].Value = "Timestamp";
                        worksheet.Cells[1, 2].Value = "Kode Item";
                        worksheet.Cells[1, 3].Value = "Jml Box";
                        worksheet.Cells[1, 4].Value = "Production Date";
                        worksheet.Cells[1, 5].Value = "Qty Pcs";

                        // Add sample data
                        worksheet.Cells[2, 1].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        worksheet.Cells[2, 2].Value = "ITEM001";
                        worksheet.Cells[2, 3].Value = 10;
                        worksheet.Cells[2, 4].Value = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                        worksheet.Cells[2, 5].Value = 100;

                        worksheet.Cells[3, 1].Value = DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm:ss");
                        worksheet.Cells[3, 2].Value = "ITEM002";
                        worksheet.Cells[3, 3].Value = 5;
                        worksheet.Cells[3, 4].Value = DateTime.Now.AddDays(-25).ToString("yyyy-MM-dd");
                        worksheet.Cells[3, 5].Value = 50;

                        // Style the header
                        using (var range = worksheet.Cells[1, 1, 1, 5])
                        {
                            range.Style.Font.Bold = true;
                            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        }

                        // Auto-fit columns
                        worksheet.Cells.AutoFitColumns();

                        var fileName = $"RVI_Supply_Log_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        var fileBytes = package.GetAsByteArray();

                        return File(fileBytes, contentType, fileName);
                    }
                    else
                    {
                        // RVI Items Template - Column headers for RVI items table
                        worksheet.Cells[1, 1].Value = "ItemCode";
                        worksheet.Cells[1, 2].Value = "QtyPerBox";
                        worksheet.Cells[1, 3].Value = "StandardMin";
                        worksheet.Cells[1, 4].Value = "StandardMax";

                        // Add sample data
                        worksheet.Cells[2, 1].Value = "ITEM001";
                        worksheet.Cells[2, 2].Value = 50.00;
                        worksheet.Cells[2, 3].Value = 100;
                        worksheet.Cells[2, 4].Value = 500;

                        worksheet.Cells[3, 1].Value = "ITEM002";
                        worksheet.Cells[3, 2].Value = 25.00;
                        worksheet.Cells[3, 3].Value = 200;
                        worksheet.Cells[3, 4].Value = 800;

                        // Style the header
                        using (var range = worksheet.Cells[1, 1, 1, 4])
                        {
                            range.Style.Font.Bold = true;
                            range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                        }

                        // Auto-fit columns
                        worksheet.Cells.AutoFitColumns();

                        var fileName = $"RVI_Items_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                        var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        var fileBytes = package.GetAsByteArray();

                        return File(fileBytes, contentType, fileName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating RVI Excel template");
                    return BadRequest($"Error generating template: {ex.Message}");
                }
            }

            [HttpGet]
            public async Task<JsonResult> GetItemsWithStockComparison()
            {
                try
                {
                    var allItems = await _context.ItemsRVI.ToListAsync();
                    var allStockSummaries = await _context.StockSummaryRVI.ToListAsync();

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
                            .Select(i => new { i.ItemCode })
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

            // Logout
            public IActionResult Logout()
            {
                HttpContext.Session.Clear();
                _logger.LogInformation("RVI User logged out");
                return RedirectToAction("Logout", "Account");
            }

            // Helper methods - adapted for RVI (no expiry tracking)
            private string DetermineItemStatus(int currentBoxStock, DateTime lastUpdated, ItemRVI item)
            {
                // RVI status logic: shortage (≤standard_min), normal (>standard_min & <standard_max), overstock (≥standard_max)
                if (IsShortage(currentBoxStock, item.StandardMin))
                    return "Shortage";
                if (IsOverStock(currentBoxStock, item.StandardMax))
                    return "Over Stock";
                if (IsNormal(currentBoxStock, item.StandardMin, item.StandardMax))
                    return "Normal";
                
                return "Normal"; // Default to Normal if no standard values
            }

            private bool IsShortage(int currentStock, int? standardMin)
            {
                // Shortage: current stock <= standard_min
                if (!standardMin.HasValue) return false;
                return currentStock <= standardMin.Value;
            }

            private bool IsNormal(int currentStock, int? standardMin, int? standardMax)
            {
                // Normal: current stock > standard_min AND current stock < standard_max
                if (!standardMin.HasValue || !standardMax.HasValue) return false;
                return currentStock > standardMin.Value && currentStock < standardMax.Value;
            }

            private bool IsOverStock(int currentStock, int? standardMax)
            {
                // Over Stock: current stock >= standard_max
                if (!standardMax.HasValue) return false;
                return currentStock >= standardMax.Value;
            }


        // ==========================================
        // RVI INPUT FEATURES
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> RVIInput()
        {
            var today = DateTime.Now.ToString("dd-MM-yyyy");
            ViewBag.Date = today;
            
            // Get Items for Datalist
            var items = await _context.ItemsRVI
                .Select(i => i.ItemCode)
                .Distinct()
                .OrderBy(i => i)
                .ToListAsync();
            
            ViewBag.ItemCodes = items;
            
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitRVIInput(string transactionType, string itemCode, string fullQr, DateTime? productionDate, int boxCount, int qtyPcs, string toProcess = "Production")
        {
            try
            {
                var today = DateTime.Now.ToString("dd/MM/yyyy");

                // Validate input
                if (string.IsNullOrEmpty(itemCode))
                    return Json(new { success = false, message = "Item Code is required" });

                if (transactionType == "IN")
                {
                    var log = new StorageLogRVI
                    {
                        ItemCode = itemCode,
                        FullQR = fullQr ?? "-",
                        ProductionDate = productionDate,
                        BoxCount = boxCount,
                        QtyPcs = qtyPcs,
                        Tanggal = today,
                        StoredAt = DateTime.Now
                    };
                    _context.StorageLogRVI.Add(log);
                }
                else // OUT
                {
                    var log = new SupplyLogRVI
                    {
                        ItemCode = itemCode,
                        // For OUT transaction, we might not always have FullQR if manually input
                        FullQR = fullQr ?? "-", 
                        BoxCount = boxCount,
                        QtyPcs = qtyPcs,
                        Tanggal = today,
                        SuppliedAt = DateTime.Now,
                        ProductionDate = productionDate
                    };
                    _context.SupplyLogRVI.Add(log);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Data saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting RVI input");
                return Json(new { success = false, message = "Error saving data: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetRVIStockForFIFO()
        {
            try
            {
                // Get stock data for FIFO suggestions
                // Prioritize older stock (First In First Out)
                var stock = await _context.StockSummaryRVI
                    .Where(s => s.CurrentBoxStock > 0)
                    .Select(s => new {
                        ItemCode = s.ItemCode,
                        FullQR = s.FullQr,
                        // Use parsed date if available, otherwise current date fallback
                        ProductionDate = s.LastUpdated, 
                        BoxCount = s.CurrentBoxStock
                    })
                    .ToListAsync();
                
                // Sort client-side because of string date parsing complexity in SQL
                // Sort in memory with date parsing
                var sortedStock = stock
                    .OrderBy(s => {
                        if (DateTime.TryParse(s.ProductionDate, out DateTime dt)) return dt;
                        return DateTime.MinValue;
                    })
                    .ToList();


                return Json(new { success = true, data = sortedStock });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }
}
