using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DashboardWIPHouse.Controllers
{
    [Authorize]
    public class InputMenuController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Input Menu";
            
            // Get database from user claims
            var database = User.FindFirst("Database")?.Value ?? "HOSE";
            ViewBag.Database = database;
            
            return View();
        }
    }
}
