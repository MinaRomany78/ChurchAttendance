using System.ComponentModel.DataAnnotations;
using ChurchAttendance.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ChurchAttendance.ViewModels;

public class SessionListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? StageId { get; set; }

    /// <summary>all | open | closed</summary>
    public string Status { get; set; } = "all";
    public int Page { get; set; } = 1;
}

public class SessionListItem
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public SessionType Type { get; set; }
    public string? StageName { get; set; }
    public string? GroupName { get; set; }
    public bool IsClosed { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public int TotalRecords { get; set; }
}

public class SessionIndexViewModel
{
    public SessionListQuery Query { get; set; } = new();
    public PagedResult<SessionListItem> Result { get; set; } = new();
    public List<StageOption> Stages { get; set; } = new();
    public bool CanCreate { get; set; }
}

public class SessionFormViewModel
{
    [StringLength(150, ErrorMessage = "العنوان لا يزيد عن 150 حرفًا.")]
    [Display(Name = "عنوان الجلسة (اختياري)")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "يرجى اختيار تاريخ الجلسة.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "التاريخ")]
    public DateOnly? SessionDate { get; set; }

    [Required(ErrorMessage = "يرجى اختيار وقت البدء.")]
    [DataType(DataType.Time)]
    [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
    [Display(Name = "وقت البدء")]
    public TimeOnly? StartTime { get; set; }

    [Display(Name = "نوع الجلسة")]
    public SessionType SessionType { get; set; } = SessionType.Regular;

    [Display(Name = "المرحلة")]
    public int? StageId { get; set; }

    [Display(Name = "المجموعة")]
    public int? GroupId { get; set; }

    [BindNever, ValidateNever]
    public List<StageOption> Stages { get; set; } = new();

    [BindNever, ValidateNever]
    public List<GroupOption> Groups { get; set; } = new();
}

public class AttendanceRow
{
    public int RecordId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public AttendanceStatus Status { get; set; }
    public DateTime? LocalCheckIn { get; set; }
    public RegistrationMethod Method { get; set; }
    public string? Notes { get; set; }
}

public class SessionDetailsViewModel
{
    public int Id { get; set; }
    public int? StageId { get; set; }
    public string? Title { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public SessionType Type { get; set; }
    public string? StageName { get; set; }
    public string? GroupName { get; set; }
    public bool IsClosed { get; set; }

    public int PresentCount { get; set; }
    public int LateCount { get; set; }
    public int AbsentCount { get; set; }
    public int ExcusedCount { get; set; }

    /// <summary>Active students in this session's scope who have no record yet.</summary>
    public int NotRegisteredCount { get; set; }

    public bool AllowExcused { get; set; }
    public bool CanRegister { get; set; }
    public bool CanCorrect { get; set; }

    public List<AttendanceRow> Rows { get; set; } = new();
}

public class RegisterOutcome
{
    public bool Succeeded { get; set; }
    public bool IsDuplicate { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public string? StudentCode { get; set; }
    public string? StatusText { get; set; }
    public string? TimeText { get; set; }
    public int RegisteredCount { get; set; }
}

public class StudentSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public bool Registered { get; set; }
}
