namespace ChurchAttendance.Security;

/// <summary>
/// Marks a controller or action as belonging to an educational stage's environment (students, attendance,
/// points, rewards...). Enforced by StageContextFilter, which runs on every request and only acts where this
/// attribute is present — so a missing or attribute-less controller never accidentally bypasses the check by
/// omission, and controllers without it (Stages/Groups/Users/Settings/Audit admin screens, Reports) are
/// unaffected.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireStageSelectedAttribute : Attribute
{
}
