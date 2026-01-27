using Microsoft.AspNetCore.Mvc;
using dashboardWIPHouse.Data;
using DashboardWIPHouse.Models;
using dashboardWIPHouse.Models; // Add this line
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace DashboardWIPHouse.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RVIContext _rviContext;
        private readonly MoldedContext _moldedContext;
        private readonly BTRDbContext _btrContext;

        public AccountController(ApplicationDbContext context, RVIContext rviContext, MoldedContext moldedContext, BTRDbContext btrContext)
        {
            _context = context;
            _rviContext = rviContext;
            _moldedContext = moldedContext;
            _btrContext = btrContext;
        }

        

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, string database)
        {
            try
            {
                // Log login attempt
                Console.WriteLine($"=== LOGIN ATTEMPT ===");
                Console.WriteLine($"Database: {database}");
                Console.WriteLine($"Username: {username}");
                Console.WriteLine($"Password: {(string.IsNullOrEmpty(password) ? "EMPTY" : "PROVIDED")}");
                Console.WriteLine($"Timestamp: {DateTime.Now}");

                if (string.IsNullOrEmpty(database))
                {
                    Console.WriteLine("ERROR: No database selected");
                    ViewBag.Error = "Please select a database";
                    return View();
                }

                if (database == "HOSE")
                {
                    // Cek ke database HOSE
                    var user = await _context.Users
                        .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                    if (user == null)
                    {
                        ViewBag.Error = "Invalid username or password for HOSE database";
                        return View();
                    }

                    // Tentukan role
                    string role = user.Username == "admin" ? "Admin" : "User";

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, role),
                        new Claim("Database", "HOSE")
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    // Update Last_Login
                    user.LastLogin = DateTime.Now;
                    await _context.SaveChangesAsync();

                    // Redirect based on role
                    if (role == "User")
                    {
                        return RedirectToAction("Index", "InputMenu");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Home");
                    }
                }
                else if (database == "RVI")
                {
                    // Cek ke database RVI
                    var user = await _rviContext.UsersRVI
                        .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                    if (user == null)
                    {
                        ViewBag.Error = "Invalid username or password for RVI database";
                        return View();
                    }

                    // Tentukan role
                    string role = user.Username == "adminRVI" ? "Admin" : "User";

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, role),
                        new Claim("Database", "RVI")
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    // Update Last_Login
                    user.LastLogin = DateTime.Now;
                    await _rviContext.SaveChangesAsync();

                    // Set session untuk RVI
                    HttpContext.Session.SetString("RVI_Username", user.Username);
                    HttpContext.Session.SetString("RVI_UserId", user.Id.ToString());

                    // Update Last_Login
                    user.LastLogin = DateTime.Now;
                    await _rviContext.SaveChangesAsync();

                    // Redirect based on role
                    if (role == "User")
                    {
                        return RedirectToAction("Index", "InputMenu");
                    }
                    else
                    {
                        return RedirectToAction("Index", "RVI");
                    }
                }
                else if (database == "MOLDED")
                {
                    // Cek ke database MOLDED
                    var user = await _moldedContext.UsersMolded
                        .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                    if (user == null)
                    {
                        ViewBag.Error = "Invalid username or password for MOLDED database";
                        return View();
                    }

                    // Tentukan role
                    string role = (user.Username == "adminMolded" || user.Username == "adminMolded321") ? "Admin" : "User";

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, role),
                        new Claim("Database", "MOLDED")
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    // Update Last_Login
                    user.LastLogin = DateTime.Now;
                    await _moldedContext.SaveChangesAsync();

                    // Set session untuk MOLDED
                    HttpContext.Session.SetString("MOLDED_Username", user.Username);
                    HttpContext.Session.SetString("MOLDED_UserId", user.Id.ToString());

                    // Update Last_Login
                    user.LastLogin = DateTime.Now;
                    await _moldedContext.SaveChangesAsync();

                    // Redirect based on role
                    if (role == "User")
                    {
                        return RedirectToAction("Index", "InputMenu");
                    }
                    else
                    {
                        return RedirectToAction("Index", "Molded");
                    }
                }
                else if (database == "BTR")
                {
                    // Cek ke database BTR
                    var user = await _btrContext.Users
                        .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);

                    if (user == null)
                    {
                        ViewBag.Error = "Invalid username or password for BTR database";
                        return View();
                    }

                    // Tentukan role
                    string role = user.Username == "adminBTR" ? "Admin" : "User";

                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.Role, role),
                        new Claim("Database", "BTR")
                    };

                    var claimsIdentity = new ClaimsIdentity(
                        claims, CookieAuthenticationDefaults.AuthenticationScheme);

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity));

                    // Update Last_Login
                    user.LastLogin = DateTime.Now;
                    await _btrContext.SaveChangesAsync();

                    // Set session untuk BTR
                    HttpContext.Session.SetString("BTRUsername", user.Username);
                    HttpContext.Session.SetInt32("BTRUserId", user.Id);

                    Console.WriteLine($"✓ BTR Login successful: {user.Username}");

                    // Redirect to BTR Dashboard
                    return RedirectToAction("Index", "BTR");
                }
                else
                {
                    ViewBag.Error = "Invalid database selection";

                    return View();
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "An error occurred during login. Please try again.";
                return View();
            }
        }




        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content("Access Denied: You do not have permission to access this resource.");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // hapus cookie authentication
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // redirect ke halaman login
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult Test()
        {
            return View();
        }
    }
}
