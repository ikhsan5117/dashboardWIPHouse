using Microsoft.EntityFrameworkCore;
using dashboardWIPHouse.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using dashboardWIPHouse.Models;
using DashboardWIPHouse.Models;

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

// ✅ Tambah BTR DbContext
builder.Services.AddDbContext<BTRDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BTRDb")));


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
    name: "btr",
    pattern: "BTR/{action=Index}/{id?}",
    defaults: new { controller = "BTR" });


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
    
    // Seed HOSE Users if connected
    if (dbContext.Database.CanConnect())
    {
        try
        {
            // Check for 'admin'
            if (!dbContext.Users.Any(u => u.Username == "admin"))
            {
                dbContext.Users.Add(new User
                {
                    Username = "admin",
                    Password = "admin123",
                    CreatedDate = DateTime.Now
                });
                dbContext.SaveChanges();
                Console.WriteLine("✓ Created default 'admin' user");
            }

            // Check for 'user'
            if (!dbContext.Users.Any(u => u.Username == "user"))
            {
                dbContext.Users.Add(new User
                {
                    Username = "user",
                    Password = "user123",
                    CreatedDate = DateTime.Now
                });
                dbContext.SaveChanges();
                Console.WriteLine("✓ Created default 'user' user");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"! Error Seeding HOSE Users: {ex.Message}");
        }
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

            // Auto-create admin user if it doesn't exist
            try 
            {
                var adminUser = moldedContext.UsersMolded.FirstOrDefault(u => u.Username == "adminMolded");
                if (adminUser == null)
                {
                    moldedContext.UsersMolded.Add(new UserMolded 
                    { 
                        Username = "adminMolded", 
                        Password = "molded321", // Default password
                        CreatedDate = DateTime.Now 
                    });
                    moldedContext.SaveChanges();
                    Console.WriteLine("✓ Created default 'adminMolded' user");
                }
                
                var adminUser321 = moldedContext.UsersMolded.FirstOrDefault(u => u.Username == "adminMolded321");
                if (adminUser321 == null)
                {
                    moldedContext.UsersMolded.Add(new UserMolded 
                    { 
                        Username = "adminMolded321", 
                        Password = "molded321", 
                        CreatedDate = DateTime.Now 
                    });
                    moldedContext.SaveChanges();
                    Console.WriteLine("✓ Created default 'adminMolded321' user");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"! Global Error Seeding Users: {ex.Message}");
            }
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

    // Test BTR database
    var btrContext = scope.ServiceProvider.GetRequiredService<BTRDbContext>();
    try
    {
        if (btrContext.Database.CanConnect())
        {
            Console.WriteLine("✓ BTR Database connection successful");

            // Auto-create admin user if it doesn't exist
            try 
            {
                var adminUser = btrContext.Users.FirstOrDefault(u => u.Username == "adminBTR");
                if (adminUser == null)
                {
                    btrContext.Users.Add(new UserBTR
                    {
                        Username = "adminBTR",
                        Password = "BTR123",
                        CreatedDate = DateTime.Now
                    });
                    btrContext.SaveChanges();
                    Console.WriteLine("✓ Created default 'adminBTR' user (password: BTR123)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"! Error Seeding BTR Users: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("✗ BTR Database connection failed");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"✗ BTR Database connection error: {ex.Message}");
    }
}

app.Run();