using Microsoft.AspNetCore.Authorization;

namespace ChurchAttendance.Security;

public record PermissionInfo(string Policy, string Label, string[] Roles);

/// <summary>
/// One place that maps permissions to roles. Controllers use [Authorize(Policy = AppPolicies.X)]
/// so every check is enforced on the server, not just by hiding buttons.
/// The same table feeds the "Roles and permissions" page, so screen and enforcement cannot drift apart.
/// </summary>
public static class AppPolicies
{
    public const string ViewData = "ViewData";
    public const string ManageStudents = "ManageStudents";
    public const string RegisterAttendance = "RegisterAttendance";
    public const string CorrectAttendance = "CorrectAttendance";
    public const string ManagePoints = "ManagePoints";
    public const string RedeemRewards = "RedeemRewards";
    public const string ManageRewards = "ManageRewards";
    public const string ViewReports = "ViewReports";
    public const string AdminOnly = "AdminOnly";

    private const string Admin = AppRoles.SystemAdministrator;
    private const string Sup = AppRoles.Supervisor;
    private const string Officer = AppRoles.AttendanceOfficer;
    private const string Viewer = AppRoles.Viewer;

    public static readonly PermissionInfo[] Permissions =
    {
        new(ViewData, "عرض الطلاب والجلسات والنقاط والمكافآت (قراءة فقط)", new[] { Admin, Sup, Officer, Viewer }),
        new(ManageStudents, "إضافة الطلاب وتعديلهم وإيقافهم وطباعة البطاقات", new[] { Admin, Sup }),
        new(RegisterAttendance, "فتح جلسات الحضور وتسجيل الحضور وإغلاق الجلسات", new[] { Admin, Sup, Officer }),
        new(CorrectAttendance, "تعديل حالات الحضور وإعادة فتح الجلسات", new[] { Admin, Sup }),
        new(ManagePoints, "إضافة النقاط وخصمها", new[] { Admin, Sup }),
        new(RedeemRewards, "استبدال المكافآت وإدارة طلبات الاستبدال", new[] { Admin, Sup }),
        new(ManageRewards, "إدارة كتالوج المكافآت", new[] { Admin }),
        new(ViewReports, "عرض التقارير وتصديرها", new[] { Admin, Sup, Viewer }),
        new(AdminOnly, "المراحل والمجموعات وأسباب النقاط والمستخدمون والإعدادات وسجل المراجعة", new[] { Admin })
    };

    public static void Configure(AuthorizationOptions options)
    {
        foreach (var permission in Permissions)
        {
            options.AddPolicy(permission.Policy, policy => policy.RequireRole(permission.Roles));
        }
    }
}
