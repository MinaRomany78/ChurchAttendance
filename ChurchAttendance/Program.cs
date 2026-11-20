using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Security;
using ChurchAttendance.Services;
using ChurchAttendance.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---------- Identity ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = false;
        options.Lockout.MaxFailedAccessAttempts = int.MaxValue;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.Zero;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddErrorDescriber<ArabicIdentityErrorDescriber>()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "ChurchAttendance.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Re-validate security stamp every minute
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

// ---------- Authorization ----------
builder.Services.AddAuthorization(options =>
{
    AppPolicies.Configure(options);
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

// ---------- MVC & Filters ----------
builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add<ChurchAttendance.Security.StageContextFilter>();

        var m = options.ModelBindingMessageProvider;
        m.SetValueMustNotBeNullAccessor(_ => "هذا الحقل مطلوب.");
        m.SetMissingBindRequiredValueAccessor(name => $"الحقل '{name}' مطلوب.");
        m.SetAttemptedValueIsInvalidAccessor((value, name) => $"القيمة '{value}' غير صالحة للحقل '{name}'.");
        m.SetValueIsInvalidAccessor(value => $"القيمة '{value}' غير صالحة.");
        m.SetValueMustBeANumberAccessor(name => $"الحقل '{name}' يجب أن يكون رقمًا.");
        m.SetUnknownValueIsInvalidAccessor(name => $"القيمة المدخلة غير صالحة للحقل '{name}'.");
    });

builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");
builder.Services.AddLocalization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(o =>
{
    o.Cookie.Name = "ChurchAttendance.Stage";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    o.IdleTimeout = TimeSpan.FromHours(8);
});

// ---------- Application Services ----------
builder.Services.AddSingleton<IAppClock, AppClock>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IChurchInfoService, ChurchInfoService>();
builder.Services.AddSingleton<IQrCodeService, QrCodeService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IPointsService, PointsService>();
builder.Services.AddScoped<IGroupPointsService, GroupPointsService>();
builder.Services.AddScoped<IRewardService, RewardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddScoped<ILogoStorageService, LogoStorageService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddSingleton<IAccessCodeHasher, AccessCodeHasher>();
builder.Services.AddScoped<ICurrentStageAccessor, CurrentStageAccessor>();

var app = builder.Build();

// ---------- Database Migration & Seeding ----------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // استدعاء الميثود بشكل لا تزامني أمن ومع تمرير IConfiguration و Environment
        await DbInitializer.InitializeAsync(services, app.Configuration, app.Environment.IsDevelopment());
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "حدث خطأ أثناء تهيئة قاعدة البيانات.");
        Console.WriteLine($"DB Initialization Error: {ex.Message} - {ex.InnerException?.Message}");
    }
}

// ---------- Pipeline ----------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";
    await next();
});

app.UseStaticFiles();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("ar-EG")
    .AddSupportedCultures("ar-EG", "en-US")
    .AddSupportedUICultures("ar-EG", "en-US"));

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();