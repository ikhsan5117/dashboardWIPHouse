using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;

namespace dashboardWIPHouse.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ItemsController> _logger;

        public ItemsController(ApplicationDbContext context, ILogger<ItemsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Items
        public async Task<IActionResult> Index()
        {
            return View();
        }

        // GET: Items/GetItems - API untuk DataTables (Updated to Items-based)
        [HttpGet]
        public async Task<IActionResult> GetItems()
        {
            try
            {
                _logger.LogInformation("Loading items data for DataTable (Items-based)...");

                // Load dari Items table sebagai basis utama - same as HomeController
                var allItems = await _context.Items.ToListAsync();
                var allStockSummaries = await _context.StockSummary.ToListAsync();

                _logger.LogInformation($"Loaded {allItems.Count} items from Items table");
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from vw_stock_summary");

                // Group stock summaries - same logic as HomeController
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

                // Transform for DataTable display - Items-based
                var items = allItems.Select(item => 
                {
                    var hasStockData = stockSummaryGrouped.ContainsKey(item.ItemCode);
                    var stockData = hasStockData ? stockSummaryGrouped[item.ItemCode] : null;
                    var currentBoxStock = stockData?.TotalCurrentBoxStock ?? 0;
                    var lastUpdated = stockData?.LastUpdated ?? DateTime.MinValue;

                    return new
                    {
                        ItemCode = item.ItemCode,
                        Mesin = item.Mesin ?? "",
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

                _logger.LogInformation($"Returning {items.Count} items to DataTable (Items-based)");
                _logger.LogInformation($"Items with stock data: {items.Count(x => x.HasStockData)}");

                return Json(new { data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading items data (Items-based)");
                return Json(new { error = ex.Message });
            }
        }

        // POST: Items/UpdateItem - API untuk update data (Fixed)
        [HttpPost]
        public async Task<IActionResult> UpdateItem([FromBody] UpdateItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Updating item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.Items.FindAsync(request.ItemCode);
                if (existingItem == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                // Update properties - fix data types
                existingItem.Mesin = request.Mesin ?? "";
                existingItem.QtyPerBox = (decimal?)request.QtyPerBox; // Convert double to decimal
                existingItem.StandardExp = request.StandardExp;
                existingItem.StandardMin = request.StandardMin;
                existingItem.StandardMax = request.StandardMax;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Item {request.ItemCode} updated successfully");

                return Json(new { success = true, message = "Data updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: Items/CreateItem - API untuk create data (Fixed)
        [HttpPost]
        public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating new item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.Items.FindAsync(request.ItemCode);
                if (existingItem != null)
                {
                    return Json(new { success = false, message = "Item code already exists" });
                }

                var item = new Item
                {
                    ItemCode = request.ItemCode,
                    Mesin = request.Mesin ?? "",
                    QtyPerBox = (decimal?)request.QtyPerBox, // Convert double to decimal
                    StandardExp = request.StandardExp,
                    StandardMin = request.StandardMin,
                    StandardMax = request.StandardMax
                };

                _context.Items.Add(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Item {request.ItemCode} created successfully");

                return Json(new { success = true, message = "Data created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // DELETE: Items/DeleteItem - API untuk delete data (Fixed)
        [HttpPost]
        public async Task<IActionResult> DeleteItem([FromBody] DeleteItemRequest request)
        {
            try
            {
                _logger.LogInformation($"Deleting item: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var item = await _context.Items.FindAsync(request.ItemCode);
                if (item == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                _context.Items.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Item {request.ItemCode} deleted successfully");

                return Json(new { success = true, message = "Data deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting item: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Items/GetDashboardSummary - API untuk dashboard summary (Updated to Items-based)
        [HttpGet]
        public async Task<IActionResult> GetDashboardSummary()
        {
            try
            {
                _logger.LogInformation("Loading dashboard summary (Items-based)...");

                // Use same logic as HomeController - Items-based approach
                var allItems = await _context.Items.ToListAsync();
                var allStockSummaries = await _context.StockSummary.ToListAsync();

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

                // Combine Items dengan Stock Summary data
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

                // Filter items (same as HomeController)
                var filteredItems = itemsWithStockData; // Show all items

                var summary = new DashboardSummary
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => IsExpired(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => IsNearExpired(x.LastUpdated, x.Item.StandardExp)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                _logger.LogInformation($"Dashboard summary (Items-based): Total={summary.TotalItems}, " +
                    $"Expired={summary.ExpiredCount}, NearExpired={summary.NearExpiredCount}, " +
                    $"BelowMin={summary.BelowMinCount}, AboveMax={summary.AboveMaxCount}");
                _logger.LogInformation($"Items with stock data: {itemsWithStockData.Count(x => x.HasStockData)}");
                _logger.LogInformation($"Items without stock data: {itemsWithStockData.Count(x => !x.HasStockData)}");

                return Json(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard summary (Items-based)");
                return Json(new { error = ex.Message });
            }
        }

        // NEW: Get comparison data like HomeController
        [HttpGet]
        public async Task<IActionResult> GetItemsComparison()
        {
            try
            {
                var allItems = await _context.Items.ToListAsync();
                var allStockSummaries = await _context.StockSummary.ToListAsync();

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

                var comparison = new
                {
                    TotalItemsInItemsTable = allItems.Count,
                    TotalRecordsInStockView = allStockSummaries.Count,
                    UniqueItemCodesInStockView = stockSummaryGrouped.Count,
                    ItemsWithStockData = allItems.Count(i => stockSummaryGrouped.ContainsKey(i.ItemCode)),
                    ItemsWithoutStockData = allItems.Count(i => !stockSummaryGrouped.ContainsKey(i.ItemCode)),
                    StockDataWithoutItemMaster = stockSummaryGrouped.Count(kvp => !allItems.Any(i => i.ItemCode == kvp.Key)),
                    
                    // Sample items without stock data
                    ItemsWithoutStock = allItems
                        .Where(i => !stockSummaryGrouped.ContainsKey(i.ItemCode))
                        .Take(10)
                        .Select(i => new { i.ItemCode, i.Mesin, i.StandardMin, i.StandardMax })
                        .ToList(),
                    
                    // Sample stock data without item master
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

        // Helper methods - same as HomeController
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
            var nearExpiredThreshold = GetNearExpiredThreshold(standardExp.Value);
            return daysUntilExpiry >= 0 && daysUntilExpiry <= nearExpiredThreshold;
        }

        private bool IsBelowMin(int totalStock, int? standardMin)
        {
            return standardMin.HasValue && totalStock < standardMin.Value;
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
    }

    // Request models for API (Fixed)
    public class UpdateItemRequest
    {
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public double QtyPerBox { get; set; } // Changed to double to handle decimals
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    public class CreateItemRequest
    {
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public double QtyPerBox { get; set; } // Changed to double to handle decimals
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    // Added missing DeleteItemRequest
    public class DeleteItemRequest
    {
        public string ItemCode { get; set; } = string.Empty;
    }
}