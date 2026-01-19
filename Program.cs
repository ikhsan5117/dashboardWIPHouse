using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// ✅ Tambah Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
    });

// ✅ Tambah DbContext (pastikan DefaultConnection ada di appsettings.json)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Tambah RVI DbContext
builder.Services.AddDbContext<RVIContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AnotherDb")));

// ✅ Tambah MOLDED DbContext
builder.Services.AddDbContext<MoldedContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MoldedDb")));

builder.Services.AddControllersWithViews();
builder.Services.AddLogging();

// ✅ Tambah Session & HttpContextAccessor
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// ✅ Middleware Authentication + Authorization
app.UseAuthentication();
app.UseAuthorization();

// ✅ Middleware Session
app.UseSession();

app.MapControllerRoute(
    name: "rvi",
    pattern: "RVI/{action=Index}/{id?}",
    defaults: new { controller = "RVI" });

app.MapControllerRoute(
    name: "molded",
    pattern: "Molded/{action=Index}/{id?}",
    defaults: new { controller = "Molded" });

app.MapControllerRoute(
    name: "beforecheck",
    pattern: "BeforeCheck/{action=Index}/{id?}",
    defaults: new { controller = "BeforeCheck" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// 🔎 Test koneksi database saat startup
using (var scope = app.Services.CreateScope())
{
    // Test HOSE database
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    try
    {
        if (dbContext.Database.CanConnect())
        {
            Console.WriteLine("✓ HOSE Database connection successful");
        }
        else
        {
            Console.WriteLine("✗ HOSE Database connection failed");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ HOSE Database connection error: {ex.Message}");
    }

    // Test RVI database
    var rviContext = scope.ServiceProvider.GetRequiredService<RVIContext>();
    try
    {
        if (rviContext.Database.CanConnect())
        {
            Console.WriteLine("✓ RVI Database connection successful");
        }
        else
        {
            Console.WriteLine("✗ RVI Database connection failed");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ RVI Database connection error: {ex.Message}");
    }

    // Test MOLDED database
    var moldedContext = scope.ServiceProvider.GetRequiredService<MoldedContext>();
    try
    {
        if (moldedContext.Database.CanConnect())
        {
            Console.WriteLine("✓ MOLDED Database connection successful");
        }
        else
        {
            Console.WriteLine("✗ MOLDED Database connection failed");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ MOLDED Database connection error: {ex.Message}");
    }
}

app.Run();