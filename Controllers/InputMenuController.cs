using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DashboardWIPHouse.Controllers
{
    [Authorize]
    public class InputMenuController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Input Menu";
            return View();
        }
    }
}
