using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using SpaBookingWeb.Data;
using SpaBookingWeb.Models;
using SpaBookingWeb.Services;
using SpaBookingWeb.Services.Manager;
using Microsoft.AspNetCore.Authorization;

using SpaBookingWeb.Services.Interfaces;
using SpaBookingWeb.Services.Implements;
using SpaBookingWeb.Hubs;
using SpaBookingWeb.Services.Client;
using Microsoft.AspNetCore.Session;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Cấu hình Cookie Policy đơn giản nhất đễ fix lỗi Google login trên Localhost
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    // Yêu cầu sự đồng ý cookie cơ bản
    options.CheckConsentNeeded = context => false; 
    options.MinimumSameSitePolicy = SameSiteMode.None; // QUAN TRỌNG: Cho phép cross-site
    options.Secure = CookieSecurePolicy.Always; // QUAN TRỌNG: Google yêu cầu HTTPS
});

// Cấu hình cụ thể cho Cookie của Identity (External)
builder.Services.ConfigureExternalCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true; // Bắt buộc cho login
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
    .AddRoles<IdentityRole>() // Thêm dòng này
    .AddDefaultTokenProviders();
// Không ghi đè DefaultSignInScheme để Identity tự quản lý (External vs Application)
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




// Đã xóa block cũ để tránh trùng lặp

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/Account/Login"; // Đường dẫn đăng nhập
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// (Đã xóa block ConfigureApplicationCookie bị lặp)

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



// Authorization with Permission




builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache(); // BẮT BUỘC

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
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                logger.LogWarning(ex, "Database not ready, retry {Retry}/{MaxRetry}", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                    throw;

                Thread.Sleep(5000); // đợi SQL Server
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

app.UseCookiePolicy(); // Chuyển lên đây

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession(); // Session thường để sau cùng hoặc trước Auth tùy nhu cầu, nhưng sau Auth là an toàn cho dữ liệu user.

app.UseStatusCodePagesWithReExecute("/Error/{0}");

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
