using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Session;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using SpaBookingWeb.Data;
using SpaBookingWeb.Hubs;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services;
using SpaBookingWeb.Services.Client;
using SpaBookingWeb.Services.Implements;
using SpaBookingWeb.Services.Interfaces;
using SpaBookingWeb.Services.Manager;
using SpaBookingWeb.Services.Receptionist;
using SpaBookingWeb.Services.Technictian;

var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment;

// In Docker, environment variables are primary (from .env)
// In local development, use appsettings.json files
if (!env.IsProduction())
{
    var exampleSettings = Path.Combine(env.ContentRootPath, "appsettings.Example.json");
    var realSettings = Path.Combine(env.ContentRootPath, "appsettings.json");

    if (!File.Exists(realSettings))
    {
        if (File.Exists(exampleSettings))
        {
            File.Copy(exampleSettings, realSettings);
            Console.WriteLine("✔ appsettings.json was created from appsettings.Example.json");
        }
        else
        {
            throw new FileNotFoundException(
                "❌ Missing appsettings.json and appsettings.Example.json"
            );
        }
    }
}

// Configuration priority:
// 1. appsettings.json (Development)
// 2. appsettings.{Environment}.json (if exists)
// 3. Environment Variables (Docker .env overrides everything)
builder.Configuration
    .AddJsonFile("appsettings.json", optional: env.IsProduction(), reloadOnChange: true)
    .AddEnvironmentVariables();


// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure simplest Cookie Policy to fix Google login error on Localhost
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // Require basic cookie consent
    options.CheckConsentNeeded = context => false; 
    options.MinimumSameSitePolicy = SameSiteMode.None; // IMPORTANT: Allow cross-site
    options.Secure = CookieSecurePolicy.Always; // IMPORTANT: Google requires HTTPS
});

// Specific configuration for Identity Cookie (External)
builder.Services.ConfigureExternalCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Required for login
});            

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = true;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddRoles<IdentityRole>() // Add this line
    .AddDefaultTokenProviders();
// Do not override DefaultSignInScheme so Identity manages itself (External vs Application)
builder.Services.AddAuthentication()
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    options.SaveTokens = true;

    options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
    {
        OnRemoteFailure = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GoogleAuth");
            logger.LogWarning("Google OnRemoteFailure: {Failure}, Query={Query}, Cookies={Cookies}",
                context.Failure?.Message, context.Request?.QueryString.Value, context.Request?.Cookies != null ? string.Join(", ", context.Request.Cookies.Keys) : "(no cookies)");
            context.HandleResponse();
            return Task.CompletedTask;
        },
        OnCreatingTicket = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GoogleAuth");
            logger.LogInformation("Google OnCreatingTicket: Name={Name}, Claims={ClaimsCount}",
                context.Principal?.Identity?.Name, context.Principal?.Claims?.Count());
            return Task.CompletedTask;
        }
    };
});




// Deleted old block to avoid duplication

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/Account/Login"; // Login path
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// (Deleted duplicated ConfigureApplicationCookie block)

//Services Injection for Manager
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IVoucherService, VoucherService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IComboService, ComboService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
builder.Services.AddScoped<IBlogPostService, BlogPostService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();


//Services Injection for Root
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<MomoService>();


//Service for Client (Customer)
builder.Services.AddScoped<IClientHomeService, ClientHomeService>();
builder.Services.AddScoped<IServiceListService, ServiceListService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IComboListService,ComboListService>();
builder.Services.AddScoped<IPostService,PostService>();
builder.Services.AddScoped<IReviewClientService,ReviewClientService>();

// Background Services
builder.Services.AddHostedService<BookingCleanupService>();

// Đăng ký Service cho Technician
builder.Services.AddScoped<ITechnicianJobService, TechnicianJobService>();

// Đăng ký Service cho Receptionist
builder.Services.AddScoped<IReceptionistService, ReceptionistService>();
builder.Services.AddScoped<SpaBookingWeb.Areas.Receptionist.Filters.RequireReceptionistAttendanceFilter>();

// 1. Đọc cấu hình Email từ appsettings.json
builder.Services.Configure<SpaBookingWeb.Settings.EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<SpaBookingWeb.Services.IEmailSenderReceptionist, SpaBookingWeb.Services.EmailSenderReceptionist>();


builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache(); // REQUIRED

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;

});


builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages();

builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); 
    // app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        const int maxRetries = 10;
        var retryCount = 0;

        while (true)
        {
            try
            {
                dbContext.Database.Migrate();
                logger.LogInformation("Database migration completed.");

                // Seed data automatically on startup
                await DbSeeder.Initialize(scope.ServiceProvider);
                logger.LogInformation("Database seeding completed.");

                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                logger.LogWarning(ex, "Database not ready, retry {Retry}/{MaxRetry}", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                    throw;

                Thread.Sleep(5000); // wait for SQL Server
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCookiePolicy(); // Move up here

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession(); // Session usually placed last or before Auth depending on need, but after Auth is safe for user data.

app.UseStatusCodePagesWithReExecute("/Error/{0}");


app.MapAreaControllerRoute(
    name: "Technician",
    areaName: "Technician",
    pattern: "Technician/{controller=Home}/{action=Index}/{id?}");

app.MapAreaControllerRoute(
    name: "Receptionist",
    areaName: "Receptionist",
    pattern: "Receptionist/{controller=Home}/{action=Index}/{id?}");

app.MapAreaControllerRoute(
    name: "Manager",
    areaName: "Manager",
    pattern: "Manager/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=HomeClient}/{id?}");


app.MapRazorPages();

app.MapHub<NotificationHub>("/notificationHub");

app.Run();
