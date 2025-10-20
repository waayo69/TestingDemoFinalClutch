using Microsoft.AspNetCore.Identity; //for authentications and roles
using Microsoft.EntityFrameworkCore; //database access
using Microsoft.Extensions.DependencyInjection; //service registration
using System;
using System.Net; //email and stmp support
using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Linq;
using TestingDemo.Data; //project's data layer
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.IIS;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Configure application cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login"; //users go when they need to login
    options.AccessDeniedPath = "/Account/AccessDenied"; //when they lack permission
});

// Add MVC with file upload configuration
builder.Services.AddControllersWithViews(options =>
{
    // Configure file upload size limits
    options.MaxModelBindingCollectionSize = 100;
})
.AddSessionStateTempDataProvider();

// Configure file upload limits
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100MB
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
});

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddSignalR();

// Add backup services
builder.Services.AddScoped<TestingDemo.Services.IBackupService, TestingDemo.Services.BackupService>();
builder.Services.AddHostedService<TestingDemo.Services.BackupSchedulerService>();

var app = builder.Build();

// ===== Auto Migrate and Seed Roles & Admin User =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    context.Database.EnsureCreated(); // Ensure database is created

    await CreateRoles(services);
    await CreateAdminUserAsync(services);
}

// ===== Middleware Pipeline =====
app.UseStaticFiles();
app.UseRouting();

// ✅ Cache Prevention Middleware — must be before authentication
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseSession(); // Enable session support

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); //routing on controllers
app.MapDefaultControllerRoute();
app.MapHub<NotificationHub>("/notificationHub");

app.Run(); //runs the web

// ===== Helper Methods =====
async Task CreateRoles(IServiceProvider serviceProvider) //seed default roles (only if they dont exist yet)
{
    var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roleNames = { "Admin", "Finance", "User", "PlanningOfficer", "CustomerCare", "DocumentOfficer" };

    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

async Task CreateAdminUserAsync(IServiceProvider serviceProvider) //Ensures there is at least one admin user. if not, creates it with default credentials
{
    using var scope = serviceProvider.CreateScope();
    var scopedServices = scope.ServiceProvider;

    try
    {
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();

        string adminRole = "Admin";
        string adminEmail = "admin@cpcpa.com";
        string adminPassword = "Admin@123";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var newAdmin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                // Add values for all required fields
                FullName = "Admin User",
                Age = 30,
                BirthDate = DateTime.Now,
                Address = "Admin Address",
                City = "Admin City",
                State = "Admin State",
                ZipCode = "12345",
                Country = "Admin Country"
            };

            var result = await userManager.CreateAsync(newAdmin, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdmin, adminRole);
                Console.WriteLine("✅ Admin user created successfully!");
            }
            else
            {
                Console.WriteLine("❌ Failed to create admin user: " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("❌ Error seeding admin user: " + ex.Message);
    }
}

