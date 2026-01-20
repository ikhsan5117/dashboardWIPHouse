using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;
using System.Diagnostics;
using System.Globalization;
using OfficeOpenXml;

namespace dashboardWIPHouse.Controllers
{
    public class MoldedController : Controller
    {
        private readonly ILogger<MoldedController> _logger;
        private readonly MoldedContext _context;

        public MoldedController(ILogger<MoldedController> logger, MoldedContext context)
        {
            _logger = logger;
            _context = context;
        }

        // MOLDED Login is now handled by AccountController

        // GET: MOLDED Dashboard
        public async Task<IActionResult> Index()
        {
            // Check if user is logged in and has MOLDED database claim
            _logger.LogInformation($"MOLDED Index - User authenticated: {User.Identity.IsAuthenticated}");
            _logger.LogInformation($"MOLDED Index - User name: {User.Identity.Name}");
            _logger.LogInformation($"MOLDED Index - Database claim: {User.FindFirst("Database")?.Value}");
            
            if (!User.Identity.IsAuthenticated || User.FindFirst("Database")?.Value != "MOLDED")
            {
                _logger.LogWarning("MOLDED Index - Authentication failed, redirecting to login");
                return RedirectToAction("Login", "Account");
            }

            try
            {
                _logger.LogInformation("=== MOLDED Dashboard Load Started ===");

                // Test database connection
                _logger.LogInformation("Testing MOLDED database connection...");
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation($"MOLDED Database connection result: {canConnect}");

                if (!canConnect)
                {
                    throw new Exception("Cannot connect to MOLDED database");
                }

                // Load data from ItemsMolded table and StockSummaryMolded view
                _logger.LogInformation("Loading MOLDED Items data...");
                var allItems = await _context.ItemsMolded.ToListAsync();
                _logger.LogInformation($"Loaded {allItems.Count} items from MOLDED items table");

                _logger.LogInformation("Loading MOLDED Stock Summary data...");
                var allStockSummaries = await _context.StockSummaryMolded.ToListAsync();
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from MOLDED vw_stock_summary");

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

                _logger.LogInformation($"Combined data for {itemsWithStockData.Count} MOLDED items");
                _logger.LogInformation($"Items with stock data: {itemsWithStockData.Count(x => x.HasStockData)}");
                _logger.LogInformation($"Items without stock data: {itemsWithStockData.Count(x => !x.HasStockData)}");

                // Filter items berdasarkan kriteria tertentu (optional)
                var filteredItems = itemsWithStockData.ToList();

                // Calculate dashboard summary dengan status baru (same as HomeController)
                var dashboardSummary = new DashboardSummaryMolded
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => x.TotalCurrentBoxStock > 0 && IsExpired(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => x.TotalCurrentBoxStock > 0 && IsNearExpired(x.LastUpdated, x.Item.StandardExp)),
                    ShortageCount = filteredItems.Count(x => IsShortage(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                // Detailed logging for verification
                _logger.LogInformation($"MOLDED Dashboard Summary - Items Based:");
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

                ViewData["Title"] = "MOLDED Dashboard";
                _logger.LogInformation("=== MOLDED Dashboard Load Completed Successfully ===");
                return View(dashboardSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError($"=== MOLDED Dashboard Load Failed ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                var emptySummary = new DashboardSummaryMolded();
                ViewData["Title"] = "MOLDED Dashboard";
                ViewData["Error"] = $"Unable to load MOLDED dashboard data: {ex.Message}";
                return View(emptySummary);
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetDashboardData()
        {
            try
            {
                // Load dari ItemsMolded table dan StockSummaryMolded view
                var allItems = await _context.ItemsMolded.ToListAsync();
                var allStockSummaries = await _context.StockSummaryMolded.ToListAsync();

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

                var dashboardSummary = new DashboardSummaryMolded
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
                _logger.LogError(ex, "Error getting MOLDED dashboard data via API");
                return Json(new { error = "Unable to load data", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTableData()
        {
            try
            {
                _logger.LogInformation("Loading MOLDED table data for DataTables...");

                var allItems = await _context.ItemsMolded.ToListAsync();
                var allStockSummaries = await _context.StockSummaryMolded.ToListAsync();

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

                _logger.LogInformation($"Prepared {tableData.Count} MOLDED records for table display");

                return Json(new { data = tableData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading MOLDED table data");
                return Json(new { error = "Unable to load table data", details = ex.Message });
            }
        }

        [HttpPost]
        public async Task<JsonResult> UploadExcel(IFormFile file)
        {
            var result = new ExcelUploadResultMolded();

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

                _logger.LogInformation($"Processing MOLDED Excel file: {file.FileName}, Size: {file.Length} bytes");

                var excelData = await ProcessExcelFileMolded(file);
                
                if (!excelData.Any())
                {
                    result.Message = "No valid data found in Excel file";
                    return Json(result);
                }

                // Validate all rows
                var validRows = new List<ExcelRowDataMolded>();
                foreach (var row in excelData)
                {
                    if (row.IsValid)
                    {
                        validRows.Add(row);
                    }
                    else
                    {
                        result.DetailedErrors.Add(new ExcelRowErrorMolded
                        {
                            RowNumber = row.RowNumber,
                            Error = string.Join(", ", row.ValidationErrors),
                            RowData = $"ItemCode: {row.ItemCode}, QtyPerBox: {row.QtyPerBox}, StandardMin: {row.StandardMin}, StandardMax: {row.StandardMax}, StandardExp: {row.StandardExp}"
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
                var insertedCount = await InsertMoldedItemsData(validRows);
                
                result.Success = true;
                result.SuccessfulRows = insertedCount;
                result.Message = $"Successfully imported {insertedCount} records from {result.ProcessedRows} total rows";
                
                if (result.ErrorRows > 0)
                {
                    result.Message += $" ({result.ErrorRows} rows had errors and were skipped)";
                    result.Errors = result.DetailedErrors.Take(10).Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                }

                _logger.LogInformation($"MOLDED Excel upload completed: {insertedCount} successful, {result.ErrorRows} errors");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MOLDED Excel upload");
                result.Message = $"Error processing file: {ex.Message}";
                result.Errors.Add(ex.Message);
            }

            return Json(result);
        }

        private async Task<List<ExcelRowDataMolded>> ProcessExcelFileMolded(IFormFile file)
        {
            var rows = new List<ExcelRowDataMolded>();
            
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

                _logger.LogInformation($"MOLDED Excel has {rowCount} rows and {colCount} columns");

                if (rowCount <= 1)
                {
                    _logger.LogWarning("MOLDED Excel file has no data rows (only header or empty)");
                    return rows;
                }

                // Process data starting from row 2
                // A=ItemCode, B=QtyPerBox, C=StandardMin, D=StandardMax, E=StandardExp
                for (int row = 2; row <= rowCount; row++)
                {
                    var rowData = new ExcelRowDataMolded { RowNumber = row };

                    try
                    {
                        var itemCodeCell = worksheet.Cells[row, 1].Value?.ToString()?.Trim(); // A: ItemCode
                        var qtyPerBoxCell = worksheet.Cells[row, 2].Value?.ToString()?.Trim(); // B: QtyPerBox
                        var standardMinCell = worksheet.Cells[row, 3].Value?.ToString()?.Trim(); // C: StandardMin
                        var standardMaxCell = worksheet.Cells[row, 4].Value?.ToString()?.Trim(); // D: StandardMax
                        var standardExpCell = worksheet.Cells[row, 5].Value?.ToString()?.Trim(); // E: StandardExp

                        // Skip completely empty rows
                        if (string.IsNullOrEmpty(itemCodeCell) && string.IsNullOrEmpty(qtyPerBoxCell) && 
                            string.IsNullOrEmpty(standardMinCell) && string.IsNullOrEmpty(standardMaxCell) && 
                            string.IsNullOrEmpty(standardExpCell))
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

                        // Parse StandardExp
                        if (!string.IsNullOrEmpty(standardExpCell))
                        {
                            if (int.TryParse(standardExpCell.Replace(",", "").Replace(".", ""), out int standardExp))
                            {
                                if (standardExp >= 0)
                                {
                                    rowData.StandardExp = standardExp;
                                }
                                else
                                {
                                    rowData.ValidationErrors.Add("StandardExp must be a positive number or zero");
                                }
                            }
                            else
                            {
                                rowData.ValidationErrors.Add($"Invalid StandardExp format: {standardExpCell} (must be a number)");
                            }
                        }
                        else
                        {
                            rowData.ValidationErrors.Add("StandardExp is required");
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
                        _logger.LogError(ex, $"Error processing MOLDED row {row}");
                        rowData.ValidationErrors.Add($"Error processing row: {ex.Message}");
                        rows.Add(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading MOLDED Excel file");
                throw new Exception($"Error reading Excel file: {ex.Message}", ex);
            }

            _logger.LogInformation($"Processed {rows.Count} rows from MOLDED Excel file");
            return rows;
        }

        private async Task<int> InsertMoldedItemsData(List<ExcelRowDataMolded> validRows)
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
                        var existingItem = await _context.ItemsMolded.FindAsync(row.ItemCode);
                        
                        if (existingItem != null)
                        {
                            // Update existing item
                            existingItem.QtyPerBox = row.QtyPerBox;
                            existingItem.StandardMin = row.StandardMin;
                            existingItem.StandardMax = row.StandardMax;
                            existingItem.StandardExp = row.StandardExp;
                            _context.ItemsMolded.Update(existingItem);
                        }
                        else
                        {
                            // Create new item
                            var newItem = new ItemMolded
                            {
                                ItemCode = row.ItemCode,
                                QtyPerBox = row.QtyPerBox,
                                StandardMin = row.StandardMin,
                                StandardMax = row.StandardMax,
                                StandardExp = row.StandardExp
                            };
                            _context.ItemsMolded.Add(newItem);
                        }
                        
                        insertedCount++;

                        // Save in batches to avoid memory issues
                        if (insertedCount % 100 == 0)
                        {
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"MOLDED Batch saved: {insertedCount} records processed");
                        }
                    }

                    // Save remaining records
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    _logger.LogInformation($"Successfully inserted/updated {insertedCount} records into MOLDED items table");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error during MOLDED database transaction, rolling back");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inserting data into MOLDED items table");
                throw new Exception($"Database error: {ex.Message}", ex);
            }

            return insertedCount;
        }

        [HttpGet]
        public async Task<JsonResult> GetItemsWithStockComparison()
        {
            try
            {
                var allItems = await _context.ItemsMolded.ToListAsync();
                var allStockSummaries = await _context.StockSummaryMolded.ToListAsync();

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

        // GET: Molded/Items - Page for CRUD operations on Molded Items
        public IActionResult Items()
        {
            // Check if user is logged in and has MOLDED database claim
            if (!User.Identity.IsAuthenticated || User.FindFirst("Database")?.Value != "MOLDED")
            {
                _logger.LogWarning("MOLDED Items - Authentication failed, redirecting to login");
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // GET: Molded/GetMoldedItems - API for DataTables
        [HttpGet]
        public async Task<IActionResult> GetMoldedItems()
        {
            try
            {
                _logger.LogInformation("Loading Molded items data for DataTable...");

                // Load from ItemsMolded table as primary source
                var allItems = await _context.ItemsMolded.ToListAsync();
                var allStockSummaries = await _context.StockSummaryMolded.ToListAsync();

                _logger.LogInformation($"Loaded {allItems.Count} items from Molded items table");
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from Molded vw_stock_summary");

                // Group stock summaries
                var stockSummaryGrouped = allStockSummaries
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0),
                        LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue,
                        RecordCount = g.Count()
                    });

                _logger.LogInformation($"Aggregated stock data for {stockSummaryGrouped.Count} unique item codes");

                // Transform for DataTable display
                var items = allItems.Select(item => 
                {
                    var hasStockData = stockSummaryGrouped.ContainsKey(item.ItemCode);
                    var stockData = hasStockData ? stockSummaryGrouped[item.ItemCode] : null;
                    var currentBoxStock = stockData?.TotalCurrentBoxStock ?? 0;
                    var lastUpdated = stockData?.LastUpdated ?? DateTime.MinValue;

                    return new
                    {
                        ItemCode = item.ItemCode,
                        QtyPerBox = (double)(item.QtyPerBox ?? 0),
                        StandardExp = item.StandardExp ?? 0,
                        StandardMin = item.StandardMin ?? 0,
                        StandardMax = item.StandardMax ?? 0,
                        
                        // Status calculations using Items data with stock comparison
                        IsExpired = IsExpired(lastUpdated, item.StandardExp),
                        IsNearExpired = IsNearExpired(lastUpdated, item.StandardExp),
                        IsBelowMin = IsBelowMin(currentBoxStock, item.StandardMin),
                        IsAboveMax = IsAboveMax(currentBoxStock, item.StandardMax),
                        
                        // Additional info for display
                        CurrentStock = currentBoxStock,
                        DaysUntilExpiry = CalculateDaysUntilExpiry(lastUpdated, item.StandardExp),
                        HasStockData = hasStockData,
                        StockRecordCount = stockData?.RecordCount ?? 0
                    };
                }).OrderBy(x => x.ItemCode).ToList();

                _logger.LogInformation($"Returning {items.Count} Molded items to DataTable");
                _logger.LogInformation($"Items with stock data: {items.Count(x => x.HasStockData)}");

                return Json(new { data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Molded items data");
                return Json(new { error = ex.Message });
            }
        }

        // POST: Molded/UpdateMoldedItem - API for updating item
        [HttpPost]
        public async Task<IActionResult> UpdateMoldedItem([FromBody] UpdateMoldedItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Updating Molded item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.ItemsMolded.FindAsync(request.ItemCode);
                if (existingItem == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                // Update properties
                existingItem.QtyPerBox = (decimal?)request.QtyPerBox;
                existingItem.StandardExp = request.StandardExp;
                existingItem.StandardMin = request.StandardMin;
                existingItem.StandardMax = request.StandardMax;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Molded item {request.ItemCode} updated successfully");

                return Json(new { success = true, message = "Data updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Molded item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Molded/CreateMoldedItem - API for creating item
        [HttpPost]
        public async Task<IActionResult> CreateMoldedItem([FromBody] CreateMoldedItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating new Molded item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.ItemsMolded.FindAsync(request.ItemCode);
                if (existingItem != null)
                {
                    return Json(new { success = false, message = "Item code already exists" });
                }

                var item = new ItemMolded
                {
                    ItemCode = request.ItemCode,
                    QtyPerBox = (decimal?)request.QtyPerBox,
                    StandardExp = request.StandardExp,
                    StandardMin = request.StandardMin,
                    StandardMax = request.StandardMax
                };

                _context.ItemsMolded.Add(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Molded item {request.ItemCode} created successfully");

                return Json(new { success = true, message = "Data created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating Molded item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Molded/DeleteMoldedItem - API for deleting item
        [HttpPost]
        public async Task<IActionResult> DeleteMoldedItem([FromBody] DeleteMoldedItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Deleting Molded item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var item = await _context.ItemsMolded.FindAsync(request.ItemCode);
                if (item == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                _context.ItemsMolded.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Molded item {request.ItemCode} deleted successfully");

                return Json(new { success = true, message = "Data deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting Molded item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Molded/GetMoldedItemsDashboardSummary - API for dashboard summary on Items page
        [HttpGet]
        public async Task<IActionResult> GetMoldedItemsDashboardSummary()
        {
            try
            {
                _logger.LogInformation("Loading Molded Items dashboard summary...");

                var allItems = await _context.ItemsMolded.ToListAsync();
                var allStockSummaries = await _context.StockSummaryMolded.ToListAsync();

                // Group stock summaries
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

                // Combine Items with Stock Summary data
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

                var summary = new DashboardSummaryMolded
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => IsExpired(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => IsNearExpired(x.LastUpdated, x.Item.StandardExp)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                _logger.LogInformation($"Molded Items dashboard summary: Total={summary.TotalItems}, " +
                    $"Expired={summary.ExpiredCount}, NearExpired={summary.NearExpiredCount}, " +
                    $"BelowMin={summary.BelowMinCount}, AboveMax={summary.AboveMaxCount}");

                return Json(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Molded Items dashboard summary");
                return Json(new { error = ex.Message });
            }
        }

        // Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            _logger.LogInformation("MOLDED User logged out");
            return RedirectToAction("Logout", "Account");
        }

        // IMPROVED: Helper method untuk menentukan status item dengan logika yang benar (same as HomeController)
        private string DetermineItemStatus(int currentBoxStock, DateTime lastUpdated, ItemMolded item)
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

        // IMPROVED: Helper methods dengan logika yang lebih jelas (same as HomeController)
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


        // ==========================================
        // MOLDED INPUT FEATURES
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> MoldedInput()
        {
            var today = DateTime.Now.ToString("dd-MM-yyyy");
            ViewBag.Date = today;
            
            // Get Items for Datalist
            var items = await _context.ItemsMolded
                .Select(i => i.ItemCode)
                .Distinct()
                .OrderBy(i => i)
                .ToListAsync();
            
            ViewBag.ItemCodes = items;
            
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitMoldedInput(string transactionType, string itemCode, string fullQr, DateTime? productionDate, int boxCount, int qtyPcs, string toProcess = "Production")
        {
            try
            {
                var today = DateTime.Now.ToString("dd/MM/yyyy");

                // Validate input
                if (string.IsNullOrEmpty(itemCode))
                    return Json(new { success = false, message = "Item Code is required" });

                if (transactionType == "IN")
                {
                    var log = new StorageLogMolded
                    {
                        ItemCode = itemCode,
                        FullQR = fullQr ?? "-",
                        ProductionDate = productionDate,
                        BoxCount = boxCount,
                        QtyPcs = qtyPcs,
                        Tanggal = today,
                        StoredAt = DateTime.Now
                    };
                    _context.StorageLogMolded.Add(log);
                }
                else // OUT
                {
                    var log = new SupplyLogMolded
                    {
                        ItemCode = itemCode,
                        FullQR = fullQr ?? "-", 
                        BoxCount = boxCount,
                        QtyPcs = qtyPcs,
                        Tanggal = today,
                        SuppliedAt = DateTime.Now,
                        ProductionDate = productionDate
                    };
                    _context.SupplyLogMolded.Add(log);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Data saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting MOLDED input");
                return Json(new { success = false, message = "Error saving data: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetMoldedStockForFIFO()
        {
            try
            {
                // Get stock data for FIFO suggestions
                var stock = await _context.StockSummaryMolded
                    .Where(s => s.CurrentBoxStock > 0)
                    .Select(s => new {
                        ItemCode = s.ItemCode,
                        FullQR = s.FullQr,
                        ProductionDate = s.LastUpdated, 
                        BoxCount = s.CurrentBoxStock
                    })
                    .ToListAsync();
                
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

    // Request models for Molded Items CRUD operations
    public class UpdateMoldedItemRequest
    {
        public string ItemCode { get; set; } = string.Empty;
        public double QtyPerBox { get; set; }
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    public class CreateMoldedItemRequest
    {
        public string ItemCode { get; set; } = string.Empty;
        public double QtyPerBox { get; set; }
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    public class DeleteMoldedItemRequest
    {
        public string ItemCode { get; set; } = string.Empty;
    }
}
