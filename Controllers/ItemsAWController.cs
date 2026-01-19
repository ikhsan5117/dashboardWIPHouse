using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;

namespace dashboardWIPHouse.Controllers
{
    
    public class ItemsAWController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ItemsAWController> _logger;

        public ItemsAWController(ApplicationDbContext context, ILogger<ItemsAWController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: ItemsAW
        public async Task<IActionResult> Index()
        {
            return View();
        }

        // GET: ItemsAW/GetItems - API untuk DataTables (ItemsAW-based)
        [HttpGet]
        public async Task<IActionResult> GetItems()
        {
            try
            {
                _logger.LogInformation("Loading ItemsAW data for DataTable...");

                // Load dari ItemsAW table sebagai basis utama
                var allItems = await _context.ItemAW.ToListAsync();
                var allStockSummaries = await _context.StockSummaryAW.ToListAsync();

                _logger.LogInformation($"Loaded {allItems.Count} items from ItemsAW table");
                _logger.LogInformation($"Loaded {allStockSummaries.Count} records from vw_stock_summary_aw");

                // Group stock summaries - sum all records with same item_code
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

                _logger.LogInformation($"Aggregated stock data for {stockSummaryGrouped.Count} unique item codes in AW");

                // Transform for DataTable display - ItemsAW-based
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
                        
                        // Status calculations using ItemsAW data with stock comparison
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

                _logger.LogInformation($"Returning {items.Count} ItemsAW to DataTable");
                _logger.LogInformation($"ItemsAW with stock data: {items.Count(x => x.HasStockData)}");

                return Json(new { data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading ItemsAW data");
                return Json(new { error = ex.Message });
            }
        }

        // POST: ItemsAW/UpdateItem - API untuk update data
        [HttpPost]
        public async Task<IActionResult> UpdateItem([FromBody] UpdateItemAWRequest request)
        {
            try
            {
                _logger.LogInformation($"Updating ItemAW: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.ItemAW.FindAsync(request.ItemCode);
                if (existingItem == null)
                {
                    return Json(new { success = false, message = "ItemAW not found" });
                }

                // Update properties
                existingItem.Mesin = request.Mesin ?? "";
                existingItem.QtyPerBox = (decimal?)request.QtyPerBox;
                existingItem.StandardExp = request.StandardExp;
                existingItem.StandardMin = request.StandardMin;
                existingItem.StandardMax = request.StandardMax;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"ItemAW {request.ItemCode} updated successfully");

                return Json(new { success = true, message = "AW Data updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating ItemAW: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // POST: ItemsAW/CreateItem - API untuk create data
        [HttpPost]
        public async Task<IActionResult> CreateItem([FromBody] CreateItemAWRequest request)
        {
            try
            {
                _logger.LogInformation($"Creating new ItemAW: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var existingItem = await _context.ItemAW.FindAsync(request.ItemCode);
                if (existingItem != null)
                {
                    return Json(new { success = false, message = "ItemAW code already exists" });
                }

                var item = new ItemAW
                {
                    ItemCode = request.ItemCode,
                    Mesin = request.Mesin ?? "",
                    QtyPerBox = (decimal?)request.QtyPerBox,
                    StandardExp = request.StandardExp,
                    StandardMin = request.StandardMin,
                    StandardMax = request.StandardMax
                };

                _context.ItemAW.Add(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"ItemAW {request.ItemCode} created successfully");

                return Json(new { success = true, message = "AW Data created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating ItemAW: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // DELETE: ItemsAW/DeleteItem - API untuk delete data
        [HttpPost]
        public async Task<IActionResult> DeleteItem([FromBody] DeleteItemAWRequest request)
        {
            try
            {
                _logger.LogInformation($"Deleting ItemAW: {request.ItemCode}");

                if (string.IsNullOrEmpty(request.ItemCode))
                {
                    return Json(new { success = false, message = "Item code is required" });
                }

                var item = await _context.ItemAW.FindAsync(request.ItemCode);
                if (item == null)
                {
                    return Json(new { success = false, message = "ItemAW not found" });
                }

                _context.ItemAW.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"ItemAW {request.ItemCode} deleted successfully");

                return Json(new { success = true, message = "AW Data deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting ItemAW: {request.ItemCode}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: ItemsAW/GetDashboardSummary - API untuk dashboard summary
        [HttpGet]
        public async Task<IActionResult> GetDashboardSummary()
        {
            try
            {
                _logger.LogInformation("Loading dashboard summary for ItemsAW...");

                // Use ItemsAW-based approach
                var allItems = await _context.ItemAW.ToListAsync();
                var allStockSummaries = await _context.StockSummaryAW.ToListAsync();

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

                // Combine ItemsAW dengan Stock Summary data
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

                var filteredItems = itemsWithStockData; // Show all items

                var summary = new DashboardSummary
                {
                    TotalItems = filteredItems.Count,
                    ExpiredCount = filteredItems.Count(x => IsExpired(x.LastUpdated, x.Item.StandardExp)),
                    NearExpiredCount = filteredItems.Count(x => IsNearExpired(x.LastUpdated, x.Item.StandardExp)),
                    BelowMinCount = filteredItems.Count(x => IsBelowMin(x.TotalCurrentBoxStock, x.Item.StandardMin)),
                    AboveMaxCount = filteredItems.Count(x => IsAboveMax(x.TotalCurrentBoxStock, x.Item.StandardMax))
                };

                _logger.LogInformation($"Dashboard summary (ItemsAW): Total={summary.TotalItems}, " +
                    $"Expired={summary.ExpiredCount}, NearExpired={summary.NearExpiredCount}, " +
                    $"BelowMin={summary.BelowMinCount}, AboveMax={summary.AboveMaxCount}");
                _logger.LogInformation($"ItemsAW with stock data: {itemsWithStockData.Count(x => x.HasStockData)}");
                _logger.LogInformation($"ItemsAW without stock data: {itemsWithStockData.Count(x => !x.HasStockData)}");

                return Json(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard summary for ItemsAW");
                return Json(new { error = ex.Message });
            }
        }

        // Get comparison data
        [HttpGet]
        public async Task<IActionResult> GetItemsComparison()
        {
            try
            {
                var allItems = await _context.ItemAW.ToListAsync();
                var allStockSummaries = await _context.StockSummaryAW.ToListAsync();

                // Group stock summaries - sum all records with same item_code
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

                var comparison = new
                {
                    TotalItemsInItemsAWTable = allItems.Count,
                    TotalRecordsInStockAWView = allStockSummaries.Count,
                    UniqueItemCodesInStockAWView = stockSummaryGrouped.Count,
                    ItemsAWWithStockData = allItems.Count(i => stockSummaryGrouped.ContainsKey(i.ItemCode)),
                    ItemsAWWithoutStockData = allItems.Count(i => !stockSummaryGrouped.ContainsKey(i.ItemCode)),
                    StockAWDataWithoutItemMaster = stockSummaryGrouped.Count(kvp => !allItems.Any(i => i.ItemCode == kvp.Key)),
                    
                    // Sample items without stock data
                    ItemsAWWithoutStock = allItems
                        .Where(i => !stockSummaryGrouped.ContainsKey(i.ItemCode))
                        .Take(10)
                        .Select(i => new { i.ItemCode, i.Mesin, i.StandardMin, i.StandardMax })
                        .ToList(),
                    
                    // Sample stock data without item master
                    StockAWWithoutItem = stockSummaryGrouped
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

        // Helper methods - same logic as Items controller
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

        private bool IsBelowMin(int totalStock, int? standardMin)
        {
            return standardMin.HasValue && totalStock < standardMin.Value;
        }

        private bool IsAboveMax(int totalStock, int? standardMax)
        {
            return standardMax.HasValue && totalStock > standardMax.Value;
        }
    }

    // Request models for ItemsAW API
    public class UpdateItemAWRequest
    {
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public double QtyPerBox { get; set; }
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    public class CreateItemAWRequest
    {
        public string ItemCode { get; set; } = string.Empty;
        public string? Mesin { get; set; }
        public double QtyPerBox { get; set; }
        public int StandardExp { get; set; }
        public int StandardMin { get; set; }
        public int StandardMax { get; set; }
    }

    public class DeleteItemAWRequest
    {
        public string ItemCode { get; set; } = string.Empty;
    }
}