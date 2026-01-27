using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;
using System.Linq;
using System.Threading.Tasks;

namespace dashboardWIPHouse.Controllers
{
    public class BTRController : Controller
    {
        private readonly BTRDbContext _context;
        private readonly ILogger<BTRController> _logger;

        public BTRController(BTRDbContext context, ILogger<BTRController> logger)
        {
            _context = context;
            _logger = logger;
        }


        // =============================================
        // DASHBOARD
        // =============================================

        public async Task<IActionResult> Index()
        {
            if (!CheckAuth()) return RedirectToAction("Login", "Account");

            try
            {
                var summary = await CalculateDashboardSummary();
                return View(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Dashboard error");
                ViewBag.Error = ex.Message;
                return View(new DashboardSummaryBTR());
            }
        }

        // Private method to calculate dashboard summary
        private async Task<DashboardSummaryBTR> CalculateDashboardSummary()
        {
            var items = await _context.Items.ToListAsync();
            var stockSummary = await _context.StockSummary.ToListAsync();

            return new DashboardSummaryBTR
            {
                TotalItems = items.Count,
                TotalStock = stockSummary.Sum(s => s.CurrentBoxStock),
                ExpiredItems = stockSummary.Count(s => s.StatusExpired == "Expired"),
                NearExpiredItems = stockSummary.Count(s => s.StatusExpired == "Near Exp"),
                BelowMinItems = items.Count(i =>
                {
                    var currentStock = stockSummary
                        .Where(s => s.ItemCode == i.ItemCode)
                        .Sum(s => s.CurrentBoxStock);
                    return currentStock < i.StandardMin;
                }),
                NormalItems = items.Count - stockSummary.Count(s => s.StatusExpired == "Expired" || s.StatusExpired == "Near Exp")
            };
        }


        // API endpoint for dashboard summary
        [HttpGet]
        public async Task<IActionResult> GetDashboardSummary()
        {
            try
            {
                var summary = await CalculateDashboardSummary();
                return Json(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDashboardSummary error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // API endpoint for dashboard data (used by refresh)
        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var summary = await CalculateDashboardSummary();
                return Json(new
                {
                    totalItems = summary.TotalItems,
                    expiredCount = summary.ExpiredItems,
                    nearExpiredCount = summary.NearExpiredItems,
                    shortageCount = summary.BelowMinItems,
                    belowMinCount = summary.BelowMinItems,
                    aboveMaxCount = summary.NormalItems,
                    totalStock = summary.TotalStock
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDashboardData error");
                return Json(new { error = ex.Message });
            }
        }

        // API endpoint for DataTable
        [HttpGet]
        public async Task<IActionResult> GetTableData()
        {
            try
            {
                var items = await _context.Items.ToListAsync();
                var stockSummary = await _context.StockSummary.ToListAsync();

                var data = items.Select((item, index) =>
                {
                    var stocks = stockSummary.Where(s => s.ItemCode == item.ItemCode).ToList();
                    var currentBoxStock = stocks.Sum(s => s.CurrentBoxStock);
                    var currentPcsStock = currentBoxStock * item.QtyPerBox;
                    var isExpired = stocks.Any(s => s.StatusExpired == "Expired");
                    var isNearExpired = stocks.Any(s => s.StatusExpired == "Near Exp");
                    var isBelowMin = currentBoxStock < item.StandardMin;
                    var isAboveMax = currentBoxStock > item.StandardMax;

                    string status;
                    if (isExpired) status = "Already Expired";
                    else if (isNearExpired) status = "Near Expired";
                    else if (isBelowMin) status = "Shortage";
                    else if (isAboveMax) status = "Over Stock";
                    else status = "Normal";

                    return new
                    {
                        no = index + 1,
                        itemCode = item.ItemCode,
                        standardMin = item.StandardMin,
                        standardMax = item.StandardMax,
                        standardExp = item.StandardExp,
                        boxStock = currentBoxStock,
                        qtyPerBox = item.QtyPerBox,
                        pcsStock = currentPcsStock,
                        status = status
                    };
                }).ToList();

                return Json(new { data = data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTableData error");
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file, string uploadType)
        {
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "No file uploaded" });

            try
            {
                // TODO: Implement actual Excel processing logic based on uploadType
                // For now just return success to confirm endpoint handles request
                await Task.Delay(1000); // Simulate processing

                return Json(new { success = true, message = "File uploaded successfully (Simulation)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UploadExcel error");
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // =============================================
        // ITEMS MANAGEMENT
        // =============================================

        public IActionResult Items()
        {
            if (!CheckAuth()) return RedirectToAction("Login", "Account");
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> GetBTRItemsDashboardSummary()
        {
            try
            {
                var items = await _context.Items.ToListAsync();
                var stockSummary = await _context.StockSummary.ToListAsync();

                int totalItems = items.Count;
                int expiredCount = 0;
                int nearExpiredCount = 0;
                int belowMinCount = 0;

                foreach (var item in items)
                {
                    var itemStocks = stockSummary.Where(s => s.ItemCode == item.ItemCode).ToList();
                    var currentBoxStock = itemStocks.Sum(s => s.CurrentBoxStock);

                    if (itemStocks.Any(s => s.StatusExpired == "Expired")) expiredCount++;
                    if (itemStocks.Any(s => s.StatusExpired == "Near Exp")) nearExpiredCount++;
                    if (currentBoxStock < item.StandardMin) belowMinCount++;
                }

                return Json(new { totalItems, expiredCount, nearExpiredCount, belowMinCount });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBTRItemsDashboardSummary error");
                return Json(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBTRItems()
        {
            try
            {
                var items = await _context.Items.ToListAsync();
                var stockSummary = await _context.StockSummary.ToListAsync();

                var result = items.Select(item =>
                {
                    var stocks = stockSummary.Where(s => s.ItemCode == item.ItemCode).ToList();
                    var currentStock = stocks.Sum(s => s.CurrentBoxStock);
                    var isExpired = stocks.Any(s => s.StatusExpired == "Expired");
                    var isNearExpired = stocks.Any(s => s.StatusExpired == "Near Exp");
                    var isBelowMin = currentStock < item.StandardMin;

                    return new
                    {
                        item.ItemCode,
                        item.Mesin,
                        item.QtyPerBox,
                        item.StandardExp,
                        item.StandardMin,
                        item.StandardMax,
                        currentStock,
                        status = isExpired ? "Expired" : isNearExpired ? "Near Exp" : isBelowMin ? "Below Min" : "Normal",
                        isExpired,
                        isNearExpired,
                        isBelowMin
                    };
                }).ToList();

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBTRItems error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateBTRItem([FromBody] ItemBTR item)
        {
            try
            {
                if (await _context.Items.AnyAsync(i => i.ItemCode == item.ItemCode))
                {
                    return Json(new { success = false, message = "Item code already exists" });
                }

                item.CreatedAt = DateTime.Now;
                item.UpdatedAt = DateTime.Now;

                _context.Items.Add(item);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Item created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateBTRItem error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBTRItem([FromBody] ItemBTR item)
        {
            try
            {
                var existing = await _context.Items.FindAsync(item.ItemCode);
                if (existing == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                existing.Mesin = item.Mesin;
                existing.QtyPerBox = item.QtyPerBox;
                existing.StandardExp = item.StandardExp;
                existing.StandardMin = item.StandardMin;
                existing.StandardMax = item.StandardMax;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Item updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateBTRItem error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteBTRItem(string itemCode)
        {
            try
            {
                var item = await _context.Items.FindAsync(itemCode);
                if (item == null)
                {
                    return Json(new { success = false, message = "Item not found" });
                }

                _context.Items.Remove(item);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Item deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteBTRItem error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // BTR INPUT (STORAGE & SUPPLY)
        // =============================================

        public IActionResult BTRInput()
        {
            if (!CheckAuth()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetItemCodes(string search = "")
        {
            try
            {
                var items = await _context.Items
                    .Where(i => string.IsNullOrEmpty(search) || i.ItemCode.Contains(search))
                    .Select(i => new { id = i.ItemCode, text = i.ItemCode })
                    .Take(20)
                    .ToListAsync();

                return Json(new { success = true, results = items });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFullQRCodes(string search = "")
        {
            try
            {
                var raks = await _context.Raks
                    .Where(r => string.IsNullOrEmpty(search) || r.FullQR.Contains(search))
                    .Select(r => new { id = r.FullQR, text = r.FullQR })
                    .Take(20)
                    .ToListAsync();

                return Json(new { success = true, results = raks });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetBTRStockForFIFO()
        {
            try
            {
                var stocks = await _context.StockSummary
                    .Where(s => s.CurrentBoxStock > 0)
                    .OrderBy(s => s.StoredAt)
                    .Select(s => new
                    {
                        s.ItemCode,
                        s.FullQR,
                        productionDate = s.StoredAt.ToString("yyyy-MM-dd"),
                        boxCount = s.CurrentBoxStock
                    })
                    .ToListAsync();

                return Json(new { success = true, data = stocks });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitBTRInput([FromBody] BTRInputRequest request)
        {
            try
            {
                if (request.TransactionType == "IN")
                {
                    // Storage Log (IN)
                    var storageLog = new StorageLogBTR
                    {
                        ItemCode = request.ItemCode,
                        FullQR = request.FullQR,
                        ProductionDate = request.ProductionDate,
                        BoxCount = request.BoxCount,
                        QtyPcs = request.QtyPcs,
                        StoredAt = DateTime.Now,
                        Tanggal = DateTime.Today
                    };

                    _context.StorageLogs.Add(storageLog);
                }
                else if (request.TransactionType == "OUT")
                {
                    // Supply Log (OUT)
                    var supplyLog = new SupplyLogBTR
                    {
                        ItemCode = request.ItemCode,
                        FullQR = request.FullQR,
                        ProductionDate = request.ProductionDate,
                        BoxCount = request.BoxCount,
                        QtyPcs = request.QtyPcs,
                        SuppliedAt = DateTime.Now,
                        ToProcess = request.ToProcess,
                        Tanggal = DateTime.Today,
                        StorageLogId = request.StorageLogId
                    };

                    _context.SupplyLogs.Add(supplyLog);
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"BTR {request.TransactionType} recorded successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubmitBTRInput error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // HISTORY & STOCK
        // =============================================

        public IActionResult History()
        {
            if (!CheckAuth()) return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoryData(string type = "storage")
        {
            try
            {
                if (type == "storage")
                {
                    var data = await _context.StorageLogs
                        .OrderByDescending(s => s.StoredAt)
                        .Select(s => new
                        {
                            s.LogId,
                            s.ItemCode,
                            s.FullQR,
                            productionDate = s.ProductionDate.HasValue ? s.ProductionDate.Value.ToString("yyyy-MM-dd") : "",
                            s.BoxCount,
                            s.QtyPcs,
                            storedAt = s.StoredAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            tanggal = s.Tanggal.ToString("yyyy-MM-dd")
                        })
                        .ToListAsync();

                    return Json(new { success = true, data });
                }
                else
                {
                    var data = await _context.SupplyLogs
                        .OrderByDescending(s => s.SuppliedAt)
                        .Select(s => new
                        {
                            s.LogId,
                            s.ItemCode,
                            s.FullQR,
                            productionDate = s.ProductionDate.HasValue ? s.ProductionDate.Value.ToString("yyyy-MM-dd") : "",
                            s.BoxCount,
                            s.QtyPcs,
                            suppliedAt = s.SuppliedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            s.ToProcess,
                            tanggal = s.Tanggal.ToString("yyyy-MM-dd"),
                            s.StorageLogId
                        })
                        .ToListAsync();

                    return Json(new { success = true, data });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetHistoryData error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHistory(int logId, string type)
        {
            try
            {
                if (type == "storage")
                {
                    var log = await _context.StorageLogs.FindAsync(logId);
                    if (log != null)
                    {
                        _context.StorageLogs.Remove(log);
                    }
                }
                else
                {
                    var log = await _context.SupplyLogs.FindAsync(logId);
                    if (log != null)
                    {
                        _context.SupplyLogs.Remove(log);
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Record deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteHistory error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // HELPER METHODS
        // =============================================

        private bool CheckAuth()
        {
            return HttpContext.Session.GetString("BTRUsername") != null;
        }
    }

    // Request Models
    public class BTRInputRequest
    {
        public string TransactionType { get; set; } // "IN" or "OUT"
        public string ItemCode { get; set; }
        public string? FullQR { get; set; }
        public DateTime? ProductionDate { get; set; }
        public int BoxCount { get; set; }
        public int QtyPcs { get; set; }
        public string? ToProcess { get; set; } // For OUT only
        public int? StorageLogId { get; set; } // For FIFO tracking
    }
}
