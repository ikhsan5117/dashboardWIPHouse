using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;
using System.Security.Claims;

namespace dashboardWIPHouse.Controllers
{
    [Authorize(Roles = "Admin")]
    public class BeforeCheckController : Controller
    {
        private readonly RVIContext _context;
        private readonly ILogger<BeforeCheckController> _logger;

        public BeforeCheckController(RVIContext context, ILogger<BeforeCheckController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            _logger.LogInformation($"Before Check Index - User authenticated: {User.Identity.IsAuthenticated}");
            _logger.LogInformation($"Before Check Index - User name: {User.Identity.Name}");
            _logger.LogInformation($"Before Check Index - Database claim: {User.FindFirst("Database")?.Value ?? "null"}");

            if (!User.Identity.IsAuthenticated || User.FindFirst("Database")?.Value != "RVI")
            {
                _logger.LogWarning("Before Check Index - Authentication failed, redirecting to login");
                return RedirectToAction("Login", "Account");
            }

            try
            {
                _logger.LogInformation("=== Before Check Dashboard Load Started ===");

                // Load from ItemsBCRVI table (Before Check)
                var allBcItems = await _context.ItemsBCRVI.ToListAsync();

                _logger.LogInformation($"Loaded {allBcItems.Count()} BC items from database");

                // Calculate summary statistics
                var summary = new BeforeCheckSummaryRVI
                {
                    TotalRaks = allBcItems.Count(),
                    TotalItems = allBcItems.Select(x => x.ItemCode).Distinct().Count(),
                    TotalQty = allBcItems.Sum(x => x.DisplayQtyPerBox),
                    FullRaks = allBcItems.Count(x => IsFullRak(x.DisplayQtyPerBox, x.DisplayStandardMax)),
                    EmptyRaks = allBcItems.Count(x => IsEmptyRak(x.DisplayQtyPerBox)),
                    PartialRaks = allBcItems.Count(x => IsPartialRak(x.DisplayQtyPerBox, x.DisplayStandardMax))
                };

                _logger.LogInformation($"Before Check Summary calculated:");
                _logger.LogInformation($"- Total Raks: {summary.TotalRaks}");
                _logger.LogInformation($"- Total Items: {summary.TotalItems}");
                _logger.LogInformation($"- Total Qty: {summary.TotalQty}");
                _logger.LogInformation($"- Full Raks: {summary.FullRaks}");
                _logger.LogInformation($"- Empty Raks: {summary.EmptyRaks}");
                _logger.LogInformation($"- Partial Raks: {summary.PartialRaks}");

                ViewData["Title"] = "Before Check Dashboard";
                _logger.LogInformation("=== Before Check Dashboard Load Completed Successfully ===");
                return View(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError($"=== Before Check Dashboard Load Failed ===");
                _logger.LogError($"Error: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");

                var emptySummary = new BeforeCheckSummaryRVI();
                ViewData["Title"] = "Before Check Dashboard";
                ViewData["Error"] = $"Unable to load Before Check dashboard data: {ex.Message}";
                return View(emptySummary);
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetTableData()
        {
            try
            {
                _logger.LogInformation("=== Before Check GetTableData Started ===");

                // Load BC items for table
                var allBcItems = await _context.ItemsBCRVI.ToListAsync();

                _logger.LogInformation($"Loaded {allBcItems.Count()} BC items for table data");

                var tableData = allBcItems.Select((bc, index) => new BeforeCheckTableItemRVI
                {
                    No = index + 1,
                    KodeRak = bc.ItemCode, // Using ItemCode as identifier
                    KodeItem = bc.ItemCode,
                    Qty = bc.DisplayQtyPerBox,
                    TypeBox = "N/A",
                    MaxCapacRak = bc.DisplayStandardMax,
                    Status = DetermineRakStatus(bc.DisplayQtyPerBox, bc.DisplayStandardMax),
                    CapacityPercentage = CalculateCapacityPercentage(bc.DisplayQtyPerBox, bc.DisplayStandardMax)
                }).ToList();

                _logger.LogInformation($"Processed {tableData.Count} items for table display");

                return Json(new { data = tableData });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetTableData: {ex.Message}");
                return Json(new { data = new List<BeforeCheckTableItemRVI>() });
            }
        }

        private bool IsFullRak(decimal qty, int maxCapacity)
        {
            if (maxCapacity <= 0) return false;
            return qty >= maxCapacity;
        }

        private bool IsEmptyRak(decimal qty)
        {
            return qty <= 0;
        }

        private bool IsPartialRak(decimal qty, int maxCapacity)
        {
            if (maxCapacity <= 0) return false;
            return qty > 0 && qty < maxCapacity;
        }

        private string DetermineRakStatus(decimal qty, int maxCapacity)
        {
            if (IsEmptyRak(qty)) return "Empty";
            if (IsFullRak(qty, maxCapacity)) return "Full";
            if (IsPartialRak(qty, maxCapacity)) return "Partial";
            return "Normal";
        }

        private double CalculateCapacityPercentage(decimal qty, int maxCapacity)
        {
            if (maxCapacity <= 0) return 0;
            return Math.Round((double)(qty / maxCapacity * 100), 1);
        }
    }
}




