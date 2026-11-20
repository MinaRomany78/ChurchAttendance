using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ChurchAttendance.ViewModels;

// ---------------------------------------------------------------- users
public class UserListItem
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsLockedOut { get; set; }
    public bool IsCurrentUser { get; set; }
}

public class UserFormViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "يرجى إدخال الاسم.")]
    [StringLength(150, ErrorMessage = "الاسم لا يزيد عن 150 حرفًا.")]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني.")]
    [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح.")]
    [StringLength(256)]
    [Display(Name = "البريد الإلكتروني (يُستخدم لتسجيل الدخول)")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى اختيار الدور.")]
    [Display(Name = "الدور")]
    public string Role { get; set; } = string.Empty;

    [Display(Name = "الحساب نشط")]
    public bool IsActive { get; set; } = true;

    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "كلمتا المرور غير متطابقتين.")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string? ConfirmPassword { get; set; }

    [BindNever, ValidateNever] public bool IsCurrentUser { get; set; }
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = string.Empty;

    [BindNever, ValidateNever] public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى إدخال كلمة المرور الجديدة.")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين.")]
    [Display(Name = "تأكيد كلمة المرور")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "يرجى إدخال كلمة المرور الحالية.")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الحالية")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى إدخال كلمة المرور الجديدة.")]
    [DataType(DataType.Password)]
    [Display(Name = "كلمة المرور الجديدة")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين.")]
    [Display(Name = "تأكيد كلمة المرور الجديدة")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// ---------------------------------------------------------------- roles
public class RoleMatrixViewModel
{
    public List<(string Role, string DisplayName, int UserCount)> Roles { get; set; } = new();
    public List<(string Label, string[] Roles)> Permissions { get; set; } = new();
}

// ---------------------------------------------------------------- settings
public class SettingsViewModel
{
    [Required(ErrorMessage = "يرجى إدخال اسم الكنيسة.")]
    [StringLength(150, ErrorMessage = "الاسم لا يزيد عن 150 حرفًا.")]
    [Display(Name = "اسم الكنيسة")]
    public string ChurchName { get; set; } = string.Empty;

    [Display(Name = "السماح بتسجيل الغياب بعذر")]
    public bool AllowExcusedAbsence { get; set; } = true;

    [Range(0, 240, ErrorMessage = "المدة بين 0 و 240 دقيقة.")]
    [Display(Name = "اعتبار الطالب متأخرًا بعد (دقيقة من بدء الجلسة، 0 لتعطيل التأخير)")]
    public int LateAfterMinutes { get; set; } = 15;

    [Display(Name = "السماح بأن يصبح رصيد الطالب سالبًا عند الخصم")]
    public bool AllowNegativePoints { get; set; }

    [BindNever, ValidateNever] public string? LogoPath { get; set; }
}

// ---------------------------------------------------------------- audit
public class AuditQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? User { get; set; }
    public string? Entity { get; set; }
    public string? Action { get; set; }
    public int Page { get; set; } = 1;
}

public class AuditLogItem
{
    public long Id { get; set; }
    public DateTime LocalTime { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActionText { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
}

public class AuditIndexViewModel
{
    public AuditQuery Query { get; set; } = new();
    public PagedResult<AuditLogItem> Result { get; set; } = new();
    public List<string> Entities { get; set; } = new();
}

// ---------------------------------------------------------------- dashboard chart
public class DashboardChartQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? StageId { get; set; }
    public int? GroupId { get; set; }
}

public class ChartDay
{
    public DateOnly Date { get; set; }
    public double? Percent { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
}
