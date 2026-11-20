namespace ChurchAttendance.Security;

public static class AppRoles
{
    public const string SystemAdministrator = "SystemAdministrator";
    public const string Supervisor = "Supervisor";
    public const string AttendanceOfficer = "AttendanceOfficer";
    public const string Viewer = "Viewer";

    public static readonly string[] All =
    {
        SystemAdministrator, Supervisor, AttendanceOfficer, Viewer
    };

    public static string DisplayName(string role) => role switch
    {
        SystemAdministrator => "مدير النظام",
        Supervisor => "مشرف / مدير",
        AttendanceOfficer => "مسؤول الحضور",
        Viewer => "مشاهد",
        _ => role
    };
}
