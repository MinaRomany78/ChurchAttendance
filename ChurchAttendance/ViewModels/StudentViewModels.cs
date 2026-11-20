using System.ComponentModel.DataAnnotations;
using ChurchAttendance.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ChurchAttendance.ViewModels;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }
    public int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public class StudentListQuery
{
    public string? Search { get; set; }
    public int? StageId { get; set; }
    public int? GroupId { get; set; }

    /// <summary>all | active | inactive</summary>
    public string Status { get; set; } = "active";

    /// <summary>name | code | stage | points | status</summary>
    public string Sort { get; set; } = "name";
    public bool Desc { get; set; }
    public int Page { get; set; } = 1;
}

public class StudentListItem
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public int Points { get; set; }
    public bool IsActive { get; set; }
}

public class StudentIndexViewModel
{
    public StudentListQuery Query { get; set; } = new();
    public PagedResult<StudentListItem> Result { get; set; } = new();
    public List<StageOption> Stages { get; set; } = new();
    public List<GroupOption> Groups { get; set; } = new();
    public bool CanManage { get; set; }
}

public class StudentFormViewModel
{
    public int? Id { get; set; }

    /// <summary>The student's stage as stored (for edit); used to guard against cross-stage access. Never bound from the form.</summary>
    [BindNever, ValidateNever]
    public int? CurrentStageId { get; set; }

    [StringLength(30, ErrorMessage = "كود الطالب لا يزيد عن 30 حرفًا.")]
    [Display(Name = "كود الطالب")]
    public string? StudentCode { get; set; }

    [Required(ErrorMessage = "يرجى إدخال اسم الطالب.")]
    [StringLength(150, ErrorMessage = "الاسم لا يزيد عن 150 حرفًا.")]
    [Display(Name = "الاسم الكامل")]
    public string FullName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "تاريخ الميلاد")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "النوع")]
    public Gender? Gender { get; set; }

    [RegularExpression(@"^[0-9٠-٩+\-\s]{6,30}$", ErrorMessage = "رقم الهاتف غير صالح.")]
    [Display(Name = "هاتف الطالب")]
    public string? Phone { get; set; }

    [StringLength(150, ErrorMessage = "الاسم لا يزيد عن 150 حرفًا.")]
    [Display(Name = "اسم ولي الأمر")]
    public string? GuardianName { get; set; }

    [RegularExpression(@"^[0-9٠-٩+\-\s]{6,30}$", ErrorMessage = "رقم الهاتف غير صالح.")]
    [Display(Name = "هاتف ولي الأمر")]
    public string? GuardianPhone { get; set; }

    [StringLength(300, ErrorMessage = "العنوان لا يزيد عن 300 حرف.")]
    [Display(Name = "العنوان")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "يرجى اختيار المرحلة.")]
    [Display(Name = "المرحلة")]
    public int? StageId { get; set; }

    [Display(Name = "المجموعة")]
    public int? GroupId { get; set; }

    [BindNever, ValidateNever]
    public List<StageOption> Stages { get; set; } = new();

    [BindNever, ValidateNever]
    public List<GroupOption> Groups { get; set; } = new();
}

public class AttendanceHistoryItem
{
    public DateOnly Date { get; set; }
    public string? SessionTitle { get; set; }
    public AttendanceStatus Status { get; set; }
    public RegistrationMethod Method { get; set; }
    public DateTime? LocalCheckIn { get; set; }
}

public class PointHistoryItem
{
    public DateTime LocalDate { get; set; }
    public PointTransactionType Type { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public class RedemptionHistoryItem
{
    public DateTime LocalDate { get; set; }
    public string RewardName { get; set; } = string.Empty;
    public int PointsUsed { get; set; }
    public RedemptionStatus Status { get; set; }
}

public class StudentDetailsViewModel
{
    public int Id { get; set; }
    public int StageId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Phone { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianPhone { get; set; }
    public string? Address { get; set; }
    public string StageName { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtLocal { get; set; }

    public int Balance { get; set; }
    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int AbsentCount { get; set; }
    public int ExcusedCount { get; set; }
    public int TotalRecords => PresentCount + LateCount + AbsentCount + ExcusedCount;

    /// <summary>(Present + Late) / (Present + Late + Absent). Excused is excluded from the base.</summary>
    public double AttendanceRate { get; set; }

    public string? QrDataUri { get; set; }
    public bool CanManage { get; set; }

    public List<AttendanceHistoryItem> Attendance { get; set; } = new();
    public List<PointHistoryItem> Transactions { get; set; } = new();
    public List<RedemptionHistoryItem> Redemptions { get; set; } = new();
}

public class BadgeViewModel
{
    public string ChurchName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string? GroupName { get; set; }
    public string QrDataUri { get; set; } = string.Empty;
}

public class StudentLookupItem
{
    public int Id { get; set; }
    public int StageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public int Balance { get; set; }
}

public class StudentPickerModel
{
    public string FieldName { get; set; } = "StudentId";
    public int? StudentId { get; set; }
    public string? Label { get; set; }
    public int? Balance { get; set; }
}
