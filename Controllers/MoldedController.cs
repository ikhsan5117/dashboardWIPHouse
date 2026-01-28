using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;
using System.Diagnostics;
using System.Globalization;
using OfficeOpenXml;

namespace dashboardWIPHouse.Controllers
{
    [Authorize]
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
        [Authorize(Roles = "Admin")]
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
                        UniqueFullQRCount = g.Select(r => r.FullQR).Distinct().Count()
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

        // GET: MOLDED Secondary Dashboard
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> IndexSecondary()
        {
            try
            {
                _logger.LogInformation("=== MOLDED Secondary Dashboard Load Started ===");

                // Test database connection
                var canConnect = await _context.Database.CanConnectAsync();
                _logger.LogInformation($"MOLDED Database connection result: {canConnect}");

                if (!canConnect)
                {
                    throw new Exception("Cannot connect to MOLDED database");
                }

                // Load MOLDED Secondary data
                _logger.LogInformation("Loading MOLDED Secondary Items data...");
                var allItemsSecondary = await _context.ItemsMoldedSecondary.ToListAsync();
                _logger.LogInformation($"Loaded {allItemsSecondary.Count} items from MOLDED items_secondary table");

                _logger.LogInformation("Loading MOLDED Secondary Stock Summary data...");
                var allStockSummariesSecondary = await _context.StockSummaryMoldedSecondary.ToListAsync();
                _logger.LogInformation($"Loaded {allStockSummariesSecondary.Count} records from MOLDED vw_stock_summary_secondary");

                // Group stock summaries by item_code
                var stockSummaryGroupedSecondary = allStockSummariesSecondary
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0),
                        LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue,
                        RecordCount = g.Count(),
                        Records = g.ToList(),
                        UniqueFullQRCount = g.Select(r => r.FullQR).Distinct().Count()
                    });

                _logger.LogInformation($"Aggregated secondary stock data for {stockSummaryGroupedSecondary.Count} unique item codes");

                // Combine Items dengan Stock Summary data
                var itemsWithStockDataSecondary = allItemsSecondary.Select(item => new
                {
                    Item = item,
                    StockData = stockSummaryGroupedSecondary.ContainsKey(item.ItemCode) 
                               ? stockSummaryGroupedSecondary[item.ItemCode] 
                               : null,
                    TotalCurrentBoxStock = stockSummaryGroupedSecondary.ContainsKey(item.ItemCode) 
                                          ? stockSummaryGroupedSecondary[item.ItemCode].TotalCurrentBoxStock 
                                          : 0,
                    LastUpdated = stockSummaryGroupedSecondary.ContainsKey(item.ItemCode) 
                                 ? stockSummaryGroupedSecondary[item.ItemCode].LastUpdated 
                                 : DateTime.MinValue,
                    HasStockData = stockSummaryGroupedSecondary.ContainsKey(item.ItemCode)
                }).ToList();

                _logger.LogInformation($"Combined data for {itemsWithStockDataSecondary.Count} MOLDED Secondary items");

                var filteredItemsSecondary = itemsWithStockDataSecondary.ToList();

                // Calculate dashboard summary
                var dashboardSummarySecondary = new DashboardSummaryMoldedSecondary
                {
                    TotalItems = filteredItemsSecondary.Count,
                    ExpiredCount = filteredItemsSecondary.Count(x => x.TotalCurrentBoxStock > 0 && IsExpiredSecondary(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItemsSecondary.Count(x => x.TotalCurrentBoxStock > 0 && IsNearExpiredSecondary(x.LastUpdated, x.Item.StandardExp)),
                    ShortageCount = filteredItemsSecondary.Count(x => IsShortageSecondary(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    BelowMinCount = filteredItemsSecondary.Count(x => IsBelowMinSecondary(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItemsSecondary.Count(x => IsAboveMaxSecondary(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                _logger.LogInformation($"MOLDED Secondary Dashboard Summary:");
                _logger.LogInformation($"- Total Items: {dashboardSummarySecondary.TotalItems}");
                _logger.LogInformation($"- Expired: {dashboardSummarySecondary.ExpiredCount}");
                _logger.LogInformation($"- Near Expired: {dashboardSummarySecondary.NearExpiredCount}");
                _logger.LogInformation($"- Shortage: {dashboardSummarySecondary.ShortageCount}");
                _logger.LogInformation($"- Below Min: {dashboardSummarySecondary.BelowMinCount}");
                _logger.LogInformation($"- Above Max: {dashboardSummarySecondary.AboveMaxCount}");

                ViewData["Title"] = "MOLDED Secondary Dashboard";
                _logger.LogInformation("=== MOLDED Secondary Dashboard Load Completed Successfully ===");
                return View(dashboardSummarySecondary);
            }
            catch (Exception ex)
            {
                _logger.LogError($"=== MOLDED Secondary Dashboard Load Failed ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                var emptySummary = new DashboardSummaryMoldedSecondary();
                ViewData["Title"] = "MOLDED Secondary Dashboard";
                ViewData["Error"] = $"Unable to load MOLDED Secondary dashboard data: {ex.Message}";
                return View(emptySummary);
            }
        }


        // GET: MOLDED Secondary Items Management
        [Authorize(Roles = "Admin")]
        public IActionResult ItemsSecondary()
        {
            // Check if user is logged in and has MOLDED database claim
            if (!User.Identity.IsAuthenticated || User.FindFirst("Database")?.Value != "MOLDED")
            {
                _logger.LogWarning("MOLDED Secondary Items - Authentication failed, redirecting to login");
                return RedirectToAction("Login", "Account");
            }

            return View();
        }

        // GET: Molded/GetMoldedSecondaryItems - API for DataTables
        [HttpGet]
        public async Task<IActionResult> GetMoldedSecondaryItems()
        {
            try
            {
                _logger.LogInformation("Loading Molded Secondary items data for DataTable...");

                // Load from ItemsMoldedSecondary table as primary source
                var allItems = await _context.ItemsMoldedSecondary.ToListAsync();
                var allStockSummaries = await _context.StockSummaryMoldedSecondary.ToListAsync();

                _logger.LogInformation($"Loaded {allItems.Count} items from Molded items_secondary table");
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from Molded vw_stock_summary_secondary");

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
                        IsExpired = IsExpiredSecondary(lastUpdated, item.StandardExp),
                        IsNearExpired = IsNearExpiredSecondary(lastUpdated, item.StandardExp),
                        IsBelowMin = IsBelowMinSecondary(currentBoxStock, item.StandardMin),
                        IsAboveMax = IsAboveMaxSecondary(currentBoxStock, item.StandardMax),
                        
                        // Additional info for display
                        CurrentStock = currentBoxStock,
                        DaysUntilExpiry = CalculateDaysUntilExpiry(lastUpdated, item.StandardExp),
                        HasStockData = hasStockData,
                        StockRecordCount = stockData?.RecordCount ?? 0
                    };
                }).OrderBy(x => x.ItemCode).ToList();

                _logger.LogInformation($"Returning {items.Count} Molded Secondary items to DataTable");
                _logger.LogInformation($"Items with stock data: {items.Count(x => x.HasStockData)}");

                return Json(new { data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Molded Secondary items data");
                return Json(new { error = ex.Message });
            }
        }

        // POST: Molded/UpdateMoldedSecondaryItem - API for updating item
        [HttpPost]
        public async Task<IActionResult> UpdateMoldedSecondaryItem([FromBody] UpdateMoldedItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Updating Molded Secondary item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.ItemsMoldedSecondary.FindAsync(request.ItemCode);
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
                _logger.LogInformation($"Molded Secondary item {request.ItemCode} updated successfully");

                return Json(new { success = true, message = "Data updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating Molded Secondary item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Molded/CreateMoldedSecondaryItem - API for creating item
        [HttpPost]
        public async Task<IActionResult> CreateMoldedSecondaryItem([FromBody] CreateMoldedItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating new Molded Secondary item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.ItemsMoldedSecondary.FindAsync(request.ItemCode);
                if (existingItem != null)
                {
                    return Json(new { success = false, message = "Item code already exists" });
                }

                var item = new ItemMoldedSecondary
                {
                    ItemCode = request.ItemCode,
                    QtyPerBox = (decimal?)request.QtyPerBox,
                    StandardExp = request.StandardExp,
                    StandardMin = request.StandardMin,
                    StandardMax = request.StandardMax
                };

                _context.ItemsMoldedSecondary.Add(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Molded Secondary item {request.ItemCode} created successfully");

                return Json(new { success = true, message = "Data created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating Molded Secondary item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Molded/DeleteMoldedSecondaryItem - API for deleting item
        [HttpPost]
        public async Task<IActionResult> DeleteMoldedSecondaryItem([FromBody] DeleteMoldedItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Deleting Molded Secondary item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var item = await _context.ItemsMoldedSecondary.FindAsync(request.ItemCode);
                if (item == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                _context.ItemsMoldedSecondary.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Molded Secondary item {request.ItemCode} deleted successfully");

                return Json(new { success = true, message = "Data deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting Molded Secondary item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Molded/GetMoldedSecondaryItemsDashboardSummary - API for dashboard summary on Items page
        [HttpGet]
        public async Task<IActionResult> GetMoldedSecondaryItemsDashboardSummary()
        {
            try
            {
                _logger.LogInformation("Loading Molded Secondary Items dashboard summary...");

                var allItems = await _context.ItemsMoldedSecondary.ToListAsync();
                var allStockSummaries = await _context.StockSummaryMoldedSecondary.ToListAsync();

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

                var summary = new DashboardSummaryMoldedSecondary
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => IsExpiredSecondary(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => IsNearExpiredSecondary(x.LastUpdated, x.Item.StandardExp)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMinSecondary(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMaxSecondary(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                _logger.LogInformation($"Molded Secondary Items dashboard summary: Total={summary.TotalItems}, " +
                    $"Expired={summary.ExpiredCount}, NearExpired={summary.NearExpiredCount}, " +
                    $"BelowMin={summary.BelowMinCount}, AboveMax={summary.AboveMaxCount}");

                return Json(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Molded Secondary Items dashboard summary");
                return Json(new { error = ex.Message });
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

                // Create case-insensitive dictionary for stock summary
                var stockSummaryDict = allStockSummaries
                    .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                    .GroupBy(s => s.ItemCode.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => new
                    {
                        TotalCurrentBoxStock = g.Sum(s => s.CurrentBoxStock ?? 0),
                        LastUpdated = g.Where(s => s.ParsedLastUpdated.HasValue)
                                     .OrderByDescending(s => s.ParsedLastUpdated)
                                     .FirstOrDefault()?.ParsedLastUpdated ?? DateTime.MinValue
                    }, StringComparer.OrdinalIgnoreCase);

                // Combine data with master list
                var itemsWithStockData = allItems.Select(item => {
                    var code = (item.ItemCode ?? "").Trim();
                    bool hasStock = stockSummaryDict.TryGetValue(code, out var stock);
                    
                    return new
                    {
                        Item = item,
                        TotalCurrentBoxStock = hasStock ? stock!.TotalCurrentBoxStock : 0,
                        LastUpdated = hasStock ? stock!.LastUpdated : DateTime.MinValue,
                        HasStockData = hasStock
                    };
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
                .Where(x => x.CurrentBoxStock > 0)
                .OrderBy(x => x.ItemCode)
                .ToList();

                _logger.LogInformation($"Prepared {tableData.Count} MOLDED records for table display");

                return Json(new { data = tableData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading MOLDED table data");
                return Json(new { success = false, data = new List<object>(), error = "Unable to load table data", details = ex.Message });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        // Now uses status_expired from database when available
        private string DetermineItemStatus(int currentBoxStock, DateTime lastUpdated, ItemMolded item, string? statusExpired = null)
        {
            // If database provides status_expired, use it for expiry-related status
            if (!string.IsNullOrEmpty(statusExpired))
            {
                if (statusExpired.Contains("expired", StringComparison.OrdinalIgnoreCase))
                    return "Already Expired";
                
                if (statusExpired.Contains("hampir", StringComparison.OrdinalIgnoreCase) || 
                    statusExpired.Contains("near", StringComparison.OrdinalIgnoreCase))
                    return "Near Expired";
            }

            // Fallback to manual calculation or stock-level status
            // Priority 1: Check for expired if stock > 0
            if (currentBoxStock > 0 && IsExpired(lastUpdated, item.StandardExp))
                return "Already Expired";

            // Priority 2: Check for near expired if stock > 0
            if (currentBoxStock > 0 && IsNearExpired(lastUpdated, item.StandardExp))
                return "Near Expired";

            // Priority 3: Check stock levels
            if (IsShortage(currentBoxStock, item.StandardMin))
                return "Shortage";

            if (IsBelowMin(currentBoxStock, item.StandardMin))
                return "Below Min";

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

        // Shortage = 0 stock
        private bool IsShortage(int currentStock, int? standardMin)
        {
            // We only count as Shortage if standardMin is set (meaning we expect stock)
            if (!standardMin.HasValue || standardMin.Value == 0) return false;
            return currentStock <= 0;
        }

        // Below Min = 0 < stock < standard min
        private bool IsBelowMin(int totalStock, int? standardMin)
        {
            if (!standardMin.HasValue || standardMin.Value == 0) return false;
            return totalStock > 0 && totalStock < standardMin.Value;
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
        // MOLDED SECONDARY HELPER METHODS
        // ==========================================

        private bool IsExpiredSecondary(DateTime lastUpdated, int? standardExp)
        {
            return IsExpired(lastUpdated, standardExp); // Same logic
        }

        private bool IsNearExpiredSecondary(DateTime lastUpdated, int? standardExp)
        {
            return IsNearExpired(lastUpdated, standardExp); // Same logic
        }

        private bool IsShortageSecondary(int currentStock, int? standardMin)
        {
            return IsShortage(currentStock, standardMin); // Same logic
        }

        private bool IsBelowMinSecondary(int totalStock, int? standardMin)
        {
            return IsBelowMin(totalStock, standardMin); // Same logic
        }

        private bool IsAboveMaxSecondary(int totalStock, int? standardMax)
        {
            return IsAboveMax(totalStock, standardMax); // Same logic
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
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteHistory(int id, string type)
        {
            try
            {
                if (type == "in")
                {
                    var log = await _context.StorageLogMolded.FindAsync(id);
                    if (log == null)
                    {
                        return Json(new { success = false, message = "Storage Log not found" });
                    }
                    _context.StorageLogMolded.Remove(log);
                    _logger.LogInformation($"Deleted Molded Storage log ID: {id}");
                }
                else if (type == "out")
                {
                    var log = await _context.SupplyLogMolded.FindAsync(id);
                    if (log == null)
                    {
                        return Json(new { success = false, message = "Supply Log not found" });
                    }
                    _context.SupplyLogMolded.Remove(log);
                    _logger.LogInformation($"Deleted Molded Supply log ID: {id}");
                }
                else
                {
                    return Json(new { success = false, message = "Invalid log type" });
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting Molded log ID: {id}, Type: {type}");
                return Json(new { success = false, message = "Error deleting log: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetItemCodes(string search = "")
        {
            try
            {
                var query = _context.ItemsMolded.AsQueryable();

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
                // 1. Get from StorageLogMolded (actual usage)
                var logList = await _context.StorageLogMolded
                    .Where(r => string.IsNullOrEmpty(search) || r.FullQR.Contains(search))
                    .Select(r => new { id = r.FullQR, text = r.FullQR })
                    .Distinct()
                    .OrderBy(r => r.text)
                    .Take(30)
                    .ToListAsync();

                // 2. Get from Raks table (master list)
                var rakList = new List<object>();
                if (_context.Raks != null)
                {
                    var rakQuery = await _context.Raks
                        .Where(r => string.IsNullOrEmpty(search) || (r.FullQR != null && r.FullQR.Contains(search)))
                        .Select(r => new { id = r.FullQR, text = r.FullQR })
                        .Distinct()
                        .OrderBy(r => r.text)
                        .Take(30)
                        .ToListAsync();
                    rakList = rakQuery.Cast<object>().ToList();
                }

                // Combine and Deduplicate
                var fullQRCodes = logList.Cast<object>()
                    .Concat(rakList)
                    .GroupBy(x => ((dynamic)x).id)
                    .Select(g => g.First())
                    .OrderBy(x => ((dynamic)x).text)
                    .Take(50)
                    .ToList();

                return Json(new { results = fullQRCodes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting full QR codes");
                return Json(new { results = new List<object>() });
            }
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
                        SuppliedAt = DateTime.Now
                        // ProductionDate removed - column doesn't exist in supply_log table
                    };
                    _context.SupplyLogMolded.Add(log);
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Data saved successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting MOLDED input");
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += " Details: " + ex.InnerException.Message;
                }
                return Json(new { success = false, message = "Error saving data: " + errorMessage });
            }
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadTemplate(string uploadType = "items")
        {
            try
            {
                _logger.LogInformation($"Generating MOLDED Excel template for upload type: {uploadType}");

                OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using var package = new OfficeOpenXml.ExcelPackage();
                var worksheet = package.Workbook.Worksheets.Add("Template");

                if (uploadType == "storage")
                {
                    // Storage Log Template
                    worksheet.Cells[1, 1].Value = "Timestamp";
                    worksheet.Cells[1, 2].Value = "Kode Rak";
                    worksheet.Cells[1, 3].Value = "Full QR";
                    worksheet.Cells[1, 4].Value = "Kode Item";
                    worksheet.Cells[1, 5].Value = "Jml Box";
                    worksheet.Cells[1, 6].Value = "Production Date";
                    worksheet.Cells[1, 7].Value = "Qty Pcs";

                    // Add sample data
                    worksheet.Cells[2, 1].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[2, 2].Value = "RAK001";
                    worksheet.Cells[2, 3].Value = "GH-ITEM001-A12-1";
                    worksheet.Cells[2, 4].Value = "ITEM001";
                    worksheet.Cells[2, 5].Value = 10;
                    worksheet.Cells[2, 6].Value = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
                    worksheet.Cells[2, 7].Value = 100;

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 7])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    }
                }
                else if (uploadType == "supply")
                {
                    // Supply Log Template
                    worksheet.Cells[1, 1].Value = "item_code";
                    worksheet.Cells[1, 2].Value = "full_qr";
                    worksheet.Cells[1, 3].Value = "box_count";
                    worksheet.Cells[1, 4].Value = "qty_pcs";
                    worksheet.Cells[1, 5].Value = "supplied_at";
                    worksheet.Cells[1, 6].Value = "to_process";

                    // Add sample data
                    worksheet.Cells[2, 1].Value = "ITEM001";
                    worksheet.Cells[2, 2].Value = "GH-ITEM001-A12-1";
                    worksheet.Cells[2, 3].Value = 5;
                    worksheet.Cells[2, 4].Value = 50;
                    worksheet.Cells[2, 5].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[2, 6].Value = "Production";

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 6])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    }
                }
                else // "items" (default)
                {
                    // Items Template
                    worksheet.Cells[1, 1].Value = "ItemCode";
                    worksheet.Cells[1, 2].Value = "QtyPerBox";
                    worksheet.Cells[1, 3].Value = "StandardMin";
                    worksheet.Cells[1, 4].Value = "StandardMax";
                    worksheet.Cells[1, 5].Value = "StandardExp";

                    // Add sample data
                    worksheet.Cells[2, 1].Value = "ITEM001";
                    worksheet.Cells[2, 2].Value = 25;
                    worksheet.Cells[2, 3].Value = 10;
                    worksheet.Cells[2, 4].Value = 50;
                    worksheet.Cells[2, 5].Value = 30;

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 5])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }
                }

                worksheet.Cells.AutoFitColumns();

                var fileName = $"Molded_{uploadType}_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                var fileBytes = package.GetAsByteArray();

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating MOLDED Excel template");
                return BadRequest($"Error generating template: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetMoldedStockForFIFO()
        {
            try
            {
                // Dynamic Calculation Strategy 
                // 1. Fetch all logs
                var inLogs = await _context.StorageLogMolded
                    .Select(s => new { s.ItemCode, s.FullQR, s.ProductionDate, s.BoxCount, s.StoredAt })
                    .ToListAsync();

                var outLogs = await _context.SupplyLogMolded
                    .Select(s => new { s.ItemCode, s.FullQR, s.BoxCount })
                    .ToListAsync();

                // 2. Group and Calculate Net Stock with case-insensitive matching
                var stock = inLogs
                    .GroupBy(x => new { 
                        ItemCode = (x.ItemCode ?? "").Trim().ToUpperInvariant(), 
                        FullQR = (x.FullQR ?? "").Trim().ToUpperInvariant() 
                    })
                    .Select(g => {
                        var totalIn = g.Sum(x => x.BoxCount);
                        
                        // Sum OUTs that match this ItemCode and FullQR (case-insensitive)
                        var totalOut = outLogs
                            .Where(x => string.Equals((x.ItemCode ?? "").Trim(), g.Key.ItemCode, StringComparison.OrdinalIgnoreCase) 
                                     && string.Equals((x.FullQR ?? "").Trim(), g.Key.FullQR, StringComparison.OrdinalIgnoreCase))
                            .Sum(x => x.BoxCount);

                        // Find oldest production date or stored date for this group
                        var bestDate = g.Min(x => x.ProductionDate ?? x.StoredAt);
                        
                        return new {
                            ItemCode = g.Key.ItemCode,
                            FullQR = g.Key.FullQR,
                            ProductionDate = bestDate.ToString("yyyy-MM-dd HH:mm:ss"),
                            RawDate = bestDate,
                            BoxCount = totalIn - totalOut
                        };
                    })
                    .Where(s => s.BoxCount > 0)
                    .OrderBy(s => s.RawDate) // FIFO sort
                    .ToList();

                return Json(new { success = true, data = stock });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Molded FIFO stock");
                return Json(new { success = false, error = ex.Message });
            }
        }

        [Authorize]
        public IActionResult History()
        {
            return View("MoldedHistory");
        }

        [HttpGet]
        public async Task<JsonResult> GetHistoryData(string type, DateTime? date = null)
        {
            try
            {
                if (type == "in")
                {
                    var query = _context.StorageLogMolded.AsQueryable();

                    if (date.HasValue)
                    {
                        var targetDate = date.Value.Date;
                        query = query.Where(x => x.StoredAt.Date == targetDate);
                    }

                    var data = await query
                        .OrderByDescending(x => x.StoredAt)
                        .Take(100)
                        .Select(x => new {
                            date = x.StoredAt.ToString("dd-MM-yyyy HH:mm"),
                            item = x.ItemCode,
                            qty = x.BoxCount,
                            qtyPcs = x.QtyPcs ?? 0,
                            status = "IN",
                            id = x.LogId
                        })
                        .ToListAsync();
                    return Json(new { success = true, data });
                }
                else if (type == "out")
                {
                    var query = _context.SupplyLogMolded.AsQueryable();

                    if (date.HasValue)
                    {
                        var targetDate = date.Value.Date;
                        query = query.Where(x => x.SuppliedAt.Date == targetDate);
                    }

                    var data = await query
                        .OrderByDescending(x => x.SuppliedAt)
                        .Take(100)
                        .Select(x => new {
                            date = x.SuppliedAt.ToString("dd-MM-yyyy HH:mm"),
                            item = x.ItemCode,
                            qty = x.BoxCount,
                            qtyPcs = x.QtyPcs ?? 0,
                            status = "OUT",
                            id = x.LogId
                        })
                        .ToListAsync();
                    return Json(new { success = true, data });
                }
                else if (type == "stock")
                {
                    var allStock = await _context.StockSummaryMolded.ToListAsync();
                    var allItemsList = await _context.ItemsMolded.ToListAsync();

                    // Create case-insensitive dictionary for master items
                    var itemsDict = allItemsList
                        .Where(x => !string.IsNullOrEmpty(x.ItemCode))
                        .ToDictionary(x => x.ItemCode.Trim(), x => x, StringComparer.OrdinalIgnoreCase);

                    // Group stock by item code (case-insensitive)
                    var stockGrouped = allStock
                        .Where(x => !string.IsNullOrEmpty(x.ItemCode))
                        .GroupBy(x => x.ItemCode.Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

                    // We want to show all items from Master list, plus any that might have stock but no master entry
                    var allCodes = allItemsList.Select(x => x.ItemCode.Trim())
                        .Union(stockGrouped.Keys, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();

                    var resultData = allCodes.Select(code => {
                        var item = itemsDict.TryGetValue(code, out var masterItem) ? masterItem : new ItemMolded { ItemCode = code, QtyPerBox = 0 };
                        var records = stockGrouped.TryGetValue(code, out var stockList) ? stockList : new List<StockSummaryMolded>();
                        
                        var totalQty = records.Sum(x => x.CurrentBoxStock ?? 0);
                        var totalPcs = (long)Math.Round((decimal)totalQty * (item.QtyPerBox ?? 0));
                        
                        var validDates = records
                            .Where(x => x.ParsedLastUpdated.HasValue)
                            .Select(x => x.ParsedLastUpdated.Value)
                            .ToList();
                        
                        var latestUpdate = validDates.Any() ? validDates.Max() : DateTime.MinValue;
                        
                        // Use database status_expired from latest record if available
                        string? statusFromDb = null;
                        if (records.Any())
                        {
                            statusFromDb = records
                                .Where(x => x.ParsedLastUpdated.HasValue)
                                .OrderByDescending(x => x.ParsedLastUpdated)
                                .FirstOrDefault()?.StatusExpired;
                        }

                        var displayStatus = DetermineItemStatus(totalQty, latestUpdate, item, statusFromDb);

                        return new {
                            itemCode = code,
                            description = displayStatus,
                            qty = totalQty,
                            qtyPcs = totalPcs,
                            lastUpdated = latestUpdate != DateTime.MinValue ? latestUpdate.ToString("dd-MM-yyyy HH:mm") : "-"
                        };
                    }).Where(x => x.qty > 0).ToList();

                    return Json(new { success = true, data = resultData });
                }
                return Json(new { success = false, message = "Invalid type" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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
