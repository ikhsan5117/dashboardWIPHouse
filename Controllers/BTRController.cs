using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using dashboardWIPHouse.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using OfficeOpenXml;

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

        [HttpGet]
        public IActionResult DownloadTemplate(string uploadType = "items")
        {
            try
            {
                _logger.LogInformation($"Generating BTR Excel template for upload type: {uploadType}");

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
                    worksheet.Cells[2, 2].Value = "RAK-BTR-01";
                    worksheet.Cells[2, 3].Value = "BTR-ITEM001-C12-1";
                    worksheet.Cells[2, 4].Value = "ITEM001";
                    worksheet.Cells[2, 5].Value = 10;
                    worksheet.Cells[2, 6].Value = DateTime.Now.AddDays(-15).ToString("yyyy-MM-dd");
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
                    worksheet.Cells[2, 2].Value = "BTR-ITEM001-C12-1";
                    worksheet.Cells[2, 3].Value = 5;
                    worksheet.Cells[2, 4].Value = 50;
                    worksheet.Cells[2, 5].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[2, 6].Value = "Trimming";

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
                    worksheet.Cells[2, 2].Value = 20;
                    worksheet.Cells[2, 3].Value = 100;
                    worksheet.Cells[2, 4].Value = 500;
                    worksheet.Cells[2, 5].Value = 60;

                    // Style the header
                    using (var range = worksheet.Cells[1, 1, 1, 5])
                    {
                        range.Style.Font.Bold = true;
                        range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    }
                }

                worksheet.Cells.AutoFitColumns();

                var fileName = $"BTR_{uploadType}_Template_{DateTime.Now:yyyyMMdd}.xlsx";
                var contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                var fileBytes = package.GetAsByteArray();

                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating BTR Excel template");
                return BadRequest($"Error generating template: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadExcel(IFormFile file, string uploadType)
        {
            var result = new ExcelUploadResultBTR();

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

                _logger.LogInformation($"Processing BTR Excel file: {file.FileName}, Type: {uploadType}, Size: {file.Length} bytes");

                var excelData = await ProcessExcelFileBTR(file, uploadType);
                
                if (!excelData.Any())
                {
                    result.Message = "No valid data found in Excel file";
                    return Json(result);
                }

                // Validate all rows
                var validRows = new List<ExcelRowDataBTR>();
                foreach (var row in excelData)
                {
                    if (row.IsValid)
                    {
                        validRows.Add(row);
                    }
                    else
                    {
                        result.DetailedErrors.Add(new ExcelRowErrorBTR
                        {
                            RowNumber = row.RowNumber,
                            Error = string.Join(", ", row.ValidationErrors),
                            RowData = uploadType == "items" ? $"ItemCode: {row.ItemCode}" : $"FullQR: {row.FullQR}, Item: {row.ItemCode}"
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

                // Insert valid data to database based on type
                int insertedCount = 0;
                if (uploadType == "items")
                {
                    insertedCount = await InsertBTRItemsData(validRows);
                }
                else if (uploadType == "storage")
                {
                    insertedCount = await InsertBTRStorageData(validRows);
                }
                else if (uploadType == "supply")
                {
                    insertedCount = await InsertBTRSupplyData(validRows);
                }

                result.Success = true;
                result.SuccessfulRows = insertedCount;
                result.Message = $"Successfully imported {insertedCount} records from {result.ProcessedRows} total rows";

                if (result.ErrorRows > 0)
                {
                    result.Message += $" ({result.ErrorRows} rows had errors and were skipped)";
                    result.Errors = result.DetailedErrors.Take(10).Select(e => $"Row {e.RowNumber}: {e.Error}").ToList();
                }

                _logger.LogInformation($"BTR Excel upload completed: {insertedCount} successful, {result.ErrorRows} errors");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing BTR Excel upload");
                result.Message = $"Error processing file: {ex.Message}";
                result.Errors.Add(ex.Message);
            }

            return Json(result);
        }

        private async Task<List<ExcelRowDataBTR>> ProcessExcelFileBTR(IFormFile file, string uploadType)
        {
            var rows = new List<ExcelRowDataBTR>();
            
            try
            {
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
                using var package = new OfficeOpenXml.ExcelPackage(stream);
                var worksheet = package.Workbook.Worksheets[0];
                
                var rowCount = worksheet.Dimension?.Rows ?? 0;

                if (rowCount <= 1) return rows;

                for (int row = 2; row <= rowCount; row++)
                {
                    var rowData = new ExcelRowDataBTR { RowNumber = row };

                    try
                    {
                        if (uploadType == "items")
                        {
                            // A=ItemCode, B=QtyPerBox, C=StandardMin, D=StandardMax, E=StandardExp
                            var itemCode = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            var qtyPerBox = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                            var stdMin = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                            var stdMax = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                            var stdExp = worksheet.Cells[row, 5].Value?.ToString()?.Trim();

                            if (string.IsNullOrEmpty(itemCode)) continue;

                            rowData.ItemCode = itemCode;
                            if (int.TryParse(qtyPerBox, out int qpb)) rowData.QtyPerBox = qpb;
                            if (int.TryParse(stdMin, out int min)) rowData.StandardMin = min;
                            if (int.TryParse(stdMax, out int max)) rowData.StandardMax = max;
                            if (int.TryParse(stdExp, out int exp)) rowData.StandardExp = exp;
                            
                            if (!rowData.QtyPerBox.HasValue) rowData.ValidationErrors.Add("Invalid QtyPerBox");
                        }
                        else if (uploadType == "storage")
                        {
                            // Timestamp, Kode Rak, Full QR, Kode Item, Jml Box, Production Date, Qty Pcs
                            var timestampStr = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            var kodeRak = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                            var fullQR = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                            var itemCode = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                            var jmlBox = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
                            var prodDateStr = worksheet.Cells[row, 6].Value?.ToString()?.Trim();
                            var qtyPcs = worksheet.Cells[row, 7].Value?.ToString()?.Trim();

                            if (string.IsNullOrEmpty(fullQR) || string.IsNullOrEmpty(itemCode)) continue;

                            rowData.FullQR = fullQR;
                            rowData.ItemCode = itemCode;
                            rowData.KodeRak = kodeRak;
                            
                            if (DateTime.TryParse(timestampStr, out DateTime ts)) rowData.Timestamp = ts;
                            else rowData.Timestamp = DateTime.Now;

                            if (int.TryParse(jmlBox, out int jb)) rowData.BoxCount = jb;
                            if (int.TryParse(qtyPcs, out int qp)) rowData.QtyPcs = qp;
                            if (DateTime.TryParse(prodDateStr, out DateTime pd)) rowData.ProductionDate = pd;
                        }
                        else if (uploadType == "supply")
                        {
                            // item_code, full_qr, box_count, qty_pcs, supplied_at, to_process
                            var itemCode = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                            var fullQR = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                            var boxCount = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                            var qtyPcs = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                            var suppliedAtStr = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
                            var toProcess = worksheet.Cells[row, 6].Value?.ToString()?.Trim();

                            if (string.IsNullOrEmpty(fullQR) || string.IsNullOrEmpty(itemCode)) continue;

                            rowData.ItemCode = itemCode;
                            rowData.FullQR = fullQR;
                            rowData.ToProcess = toProcess;

                            if (int.TryParse(boxCount, out int bc)) rowData.BoxCount = bc;
                            if (int.TryParse(qtyPcs, out int qp)) rowData.QtyPcs = qp;
                            if (DateTime.TryParse(suppliedAtStr, out DateTime sa)) rowData.Timestamp = sa;
                            else rowData.Timestamp = DateTime.Now;
                        }

                        rows.Add(rowData);
                    }
                    catch (Exception ex)
                    {
                        rowData.ValidationErrors.Add($"Row Error: {ex.Message}");
                        rows.Add(rowData);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error Processing BTR Excel");
                throw;
            }

            return rows;
        }

        private async Task<int> InsertBTRItemsData(List<ExcelRowDataBTR> validRows)
        {
            int count = 0;
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in validRows)
                {
                    var existing = await _context.Items.FindAsync(row.ItemCode);
                    if (existing != null)
                    {
                        existing.QtyPerBox = row.QtyPerBox ?? existing.QtyPerBox;
                        existing.StandardMin = row.StandardMin ?? existing.StandardMin;
                        existing.StandardMax = row.StandardMax ?? existing.StandardMax;
                        existing.StandardExp = row.StandardExp ?? existing.StandardExp;
                        existing.UpdatedAt = DateTime.Now;
                        _context.Items.Update(existing);
                    }
                    else
                    {
                        _context.Items.Add(new ItemBTR
                        {
                            ItemCode = row.ItemCode,
                            QtyPerBox = row.QtyPerBox ?? 0,
                            StandardMin = row.StandardMin ?? 0,
                            StandardMax = row.StandardMax ?? 0,
                            StandardExp = row.StandardExp ?? 0,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
                        });
                    }
                    count++;
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
            return count;
        }

        private async Task<int> InsertBTRStorageData(List<ExcelRowDataBTR> validRows)
        {
            int count = 0;
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in validRows)
                {
                    _context.StorageLogs.Add(new StorageLogBTR
                    {
                        ItemCode = row.ItemCode,
                        FullQR = row.FullQR,
                        BoxCount = row.BoxCount ?? 0,
                        QtyPcs = row.QtyPcs ?? 0,
                        ProductionDate = row.ProductionDate,
                        StoredAt = row.Timestamp ?? DateTime.Now,
                        Tanggal = DateTime.Today
                    });
                    count++;
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
            return count;
        }

        private async Task<int> InsertBTRSupplyData(List<ExcelRowDataBTR> validRows)
        {
            int count = 0;
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in validRows)
                {
                    // Find matching storage_log for FIFO if possible
                    var storageLog = await _context.StorageLogs
                        .Where(s => s.FullQR == row.FullQR)
                        .OrderByDescending(s => s.StoredAt)
                        .FirstOrDefaultAsync();

                    _context.SupplyLogs.Add(new SupplyLogBTR
                    {
                        ItemCode = row.ItemCode,
                        FullQR = row.FullQR,
                        BoxCount = row.BoxCount ?? 0,
                        QtyPcs = row.QtyPcs ?? 0,
                        ToProcess = row.ToProcess,
                        SuppliedAt = row.Timestamp ?? DateTime.Now,
                        Tanggal = DateTime.Today,
                        StorageLogId = storageLog?.LogId
                    });
                    count++;
                }
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch { await transaction.RollbackAsync(); throw; }
            return count;
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
        public async Task<IActionResult> GetHistoryData(string type, DateTime? date = null)
        {
            try
            {
                // Normalize expected values to match AfterWashing: in/out/stock
                type = (type ?? string.Empty).Trim().ToLowerInvariant();

                if (type == "in")
                {
                    var query = _context.StorageLogs.AsQueryable();

                    if (date.HasValue)
                    {
                        var targetDate = date.Value.Date;
                        query = query.Where(x => x.StoredAt.Date == targetDate);
                    }

                    var data = await query
                        .OrderByDescending(x => x.StoredAt)
                        .Take(100)
                        .Select(x => new
                        {
                            date = x.StoredAt.ToString("dd-MM-yyyy HH:mm"),
                            item = x.ItemCode,
                            qty = x.BoxCount,
                            qtyPcs = x.QtyPcs,
                            status = "IN",
                            id = x.LogId
                        })
                        .ToListAsync();

                    return Json(new { success = true, data });
                }
                else if (type == "out")
                {
                    var query = _context.SupplyLogs.AsQueryable();

                    if (date.HasValue)
                    {
                        var targetDate = date.Value.Date;
                        query = query.Where(x => x.SuppliedAt.Date == targetDate);
                    }

                    var data = await query
                        .OrderByDescending(x => x.SuppliedAt)
                        .Take(100)
                        .Select(x => new
                        {
                            date = x.SuppliedAt.ToString("dd-MM-yyyy HH:mm"),
                            item = x.ItemCode,
                            qty = x.BoxCount,
                            qtyPcs = x.QtyPcs,
                            status = "OUT",
                            id = x.LogId
                        })
                        .ToListAsync();

                    return Json(new { success = true, data });
                }
                else if (type == "stock")
                {
                    // Aggregate stock to itemCode-level (match AfterWashing response shape)
                    var allItems = await _context.Items
                        .Select(i => new { i.ItemCode, i.QtyPerBox, i.StandardMin })
                        .ToDictionaryAsync(i => i.ItemCode, StringComparer.OrdinalIgnoreCase);

                    var stockRows = await _context.StockSummary
                        .Where(s => !string.IsNullOrEmpty(s.ItemCode))
                        .ToListAsync();

                    var grouped = stockRows
                        .GroupBy(s => s.ItemCode)
                        .Select(g =>
                        {
                            var totalBox = g.Sum(x => x.CurrentBoxStock);
                            var latest = g
                                .OrderByDescending(x => x.LastUpdate)
                                .FirstOrDefault();

                            var statusExpired = latest?.StatusExpired;
                            var lastUpdated = latest?.LastUpdate ?? DateTime.MinValue;

                            allItems.TryGetValue(g.Key.Trim(), out var item);
                            var qtyPerBox = item?.QtyPerBox ?? 0;
                            var qtyPcs = totalBox * qtyPerBox;
                            var standardMin = item?.StandardMin ?? 0;

                            string displayStatus = "Normal";
                            
                            // 1. Check Expiry first
                            if (!string.IsNullOrEmpty(statusExpired))
                            {
                                if (statusExpired.Contains("Expired", StringComparison.OrdinalIgnoreCase)) displayStatus = "Already Expired";
                                else if (statusExpired.Contains("Near", StringComparison.OrdinalIgnoreCase)) displayStatus = "Near Expired";
                            }

                            // 2. Check Shortage (only if not already Expired/Near)
                            if (displayStatus == "Normal" && standardMin > 0 && totalBox < standardMin)
                            {
                                displayStatus = "Shortage";
                            }

                            return new
                            {
                                itemCode = g.Key.Trim(),
                                description = displayStatus,
                                qty = totalBox,
                                qtyPcs = qtyPcs,
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
                _logger.LogError(ex, "GetHistoryData error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetStockSummary()
        {
            try
            {
                var stockData = await _context.StockSummary
                    .OrderBy(s => s.ItemCode)
                    .Select(s => new
                    {
                        s.ItemCode,
                        s.FullQR,
                        s.CurrentBoxStock,
                        s.StatusExpired,
                        s.ExpiredDate,
                        s.LastUpdate,
                        currentPcsStock = _context.Items
                            .Where(i => i.ItemCode == s.ItemCode)
                            .Select(i => i.QtyPerBox * s.CurrentBoxStock)
                            .FirstOrDefault(),
                        standardMin = _context.Items
                            .Where(i => i.ItemCode == s.ItemCode)
                            .Select(i => i.StandardMin)
                            .FirstOrDefault()
                    })
                    .ToListAsync();

                return Json(new { success = true, data = stockData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetStockSummary error");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteHistory(int id, string type)
        {
            try
            {
                // UI sudah hide tombol untuk non-admin, tapi tetap harus dijaga di server.
                if (!User.IsInRole("Admin"))
                {
                    return Json(new { success = false, message = "Unauthorized" });
                }

                type = (type ?? string.Empty).Trim().ToLowerInvariant();

                if (type == "in")
                {
                    var log = await _context.StorageLogs.FindAsync(id);
                    if (log != null)
                    {
                        _context.StorageLogs.Remove(log);
                    }
                }
                else if (type == "out")
                {
                    var log = await _context.SupplyLogs.FindAsync(id);
                    if (log != null)
                    {
                        _context.SupplyLogs.Remove(log);
                    }
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
