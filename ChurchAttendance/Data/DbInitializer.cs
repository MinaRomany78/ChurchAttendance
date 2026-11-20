using ChurchAttendance.Models;
using ChurchAttendance.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, bool isDevelopment)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var db = sp.GetRequiredService<ApplicationDbContext>();

        if (!db.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException(
                "No EF Core migrations found. Run: dotnet ef migrations add InitialCreate");
        }

        // Development: apply migrations automatically. Production: run "dotnet ef database update" deliberately.
        if (isDevelopment)
        {
            await db.Database.MigrateAsync();
        }

        await SeedRolesAsync(sp);
        await SeedAdminAsync(sp, config, logger);
        await SeedSettingsAsync(db);
        await SeedPointReasonsAsync(db);

        if (isDevelopment)
        {
            await SeedDevelopmentDataAsync(db);
        }
    }

    private static async Task SeedRolesAsync(IServiceProvider sp)
    {
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                await roles.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
        var email = config["SeedAdmin:Email"];
        var password = config["SeedAdmin:Password"];
        var fullName = config["SeedAdmin:FullName"] ?? "مدير النظام";

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedAdmin:Email / SeedAdmin:Password not configured. No administrator was created.");
            return;
        }

        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            IsActive = true
        };

        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            logger.LogError("Could not create the seed administrator: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
            return;
        }

        await users.AddToRoleAsync(admin, AppRoles.SystemAdministrator);
        logger.LogInformation("Seed administrator {Email} created.", email);
    }

    private static async Task SeedSettingsAsync(ApplicationDbContext db)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingKeys.ChurchName] = "كنيسة مار جرجس",
            [SettingKeys.LogoPath] = "",
            [SettingKeys.AllowExcusedAbsence] = "true",
            [SettingKeys.LateAfterMinutes] = "15",
            [SettingKeys.AllowNegativePoints] = "false"
        };

        var existing = await db.ChurchSettings.Select(s => s.Key).ToListAsync();
        foreach (var (key, value) in defaults)
        {
            if (!existing.Contains(key))
            {
                db.ChurchSettings.Add(new ChurchSetting { Key = key, Value = value });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedPointReasonsAsync(ApplicationDbContext db)
    {
        if (await db.PointReasons.AnyAsync()) return;

        db.PointReasons.AddRange(
            new PointReason { Name = "حضور", DefaultPoints = 10 },
            new PointReason { Name = "نشاط", DefaultPoints = 5 },
            new PointReason { Name = "مشاركة", DefaultPoints = 5 },
            new PointReason { Name = "سلوك إيجابي", DefaultPoints = 5 },
            new PointReason { Name = "مناسبة أو نشاط خاص", DefaultPoints = 10 },
            new PointReason { Name = "مخالفة سلوكية", DefaultPoints = 5, IsDeduction = true });

        await db.SaveChangesAsync();
    }

    private static async Task SeedDevelopmentDataAsync(ApplicationDbContext db)
    {
        if (await db.Stages.AnyAsync()) return;

        db.Stages.AddRange(
            // مرحلة الروضة / الحضانة
            new Stage { Name = "حضانة 1 (KG1)", SortOrder = 1 },
            new Stage { Name = "حضانة 2 (KG2)", SortOrder = 2 },

            // المرحلة الابتدائية
            new Stage { Name = "الأول الابتدائي", SortOrder = 3 },
            new Stage { Name = "الثاني الابتدائي", SortOrder = 4 },
            new Stage { Name = "الثالث الابتدائي", SortOrder = 5 },
            new Stage { Name = "الرابع الابتدائي", SortOrder = 6 },
            new Stage { Name = "الخامس الابتدائي", SortOrder = 7 },
            new Stage { Name = "السادس الابتدائي", SortOrder = 8 },

            // المرحلة الإعدادية
            new Stage { Name = "الأول الإعدادي", SortOrder = 9 },
            new Stage { Name = "الثاني الإعدادي", SortOrder = 10 },
            new Stage { Name = "الثالث الإعدادي", SortOrder = 11 },

            // المرحلة الثانوية
            new Stage { Name = "الأول الثانوي", SortOrder = 12 },
            new Stage { Name = "الثاني الثانوي", SortOrder = 13 },
            new Stage { Name = "الثالث الثانوي", SortOrder = 14 }
        );

        await db.SaveChangesAsync();
    }
}
