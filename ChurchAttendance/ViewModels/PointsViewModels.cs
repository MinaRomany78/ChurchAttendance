using System.ComponentModel.DataAnnotations;
using ChurchAttendance.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ChurchAttendance.ViewModels;

public record ReasonOption(int Id, string Name, int DefaultPoints);

public class PointListQuery
{
    public string? Search { get; set; }
    public int? StudentId { get; set; }
    public PointTransactionType? Type { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public class PointListItem
{
    public int Id { get; set; }
    public DateTime LocalDate { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public PointTransactionType Type { get; set; }
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class PointIndexViewModel
{
    public PointListQuery Query { get; set; } = new();
    public PagedResult<PointListItem> Result { get; set; } = new();
    public int TotalAdded { get; set; }
    public int TotalDeducted { get; set; }
    public bool CanManage { get; set; }
}

public class PointFormViewModel
{
    [Required(ErrorMessage = "يرجى اختيار الطالب.")]
    public int? StudentId { get; set; }

    [Required(ErrorMessage = "يرجى اختيار سبب.")]
    [Display(Name = "السبب")]
    public int? PointReasonId { get; set; }

    [Required(ErrorMessage = "يرجى إدخال عدد النقاط.")]
    [Range(1, 10000, ErrorMessage = "عدد النقاط يجب أن يكون بين 1 و 10000.")]
    [Display(Name = "عدد النقاط")]
    public int? Points { get; set; }

    [StringLength(500, ErrorMessage = "الملاحظات لا تزيد عن 500 حرف.")]
    [Display(Name = "ملاحظات (اختياري)")]
    public string? Notes { get; set; }

    [Display(Name = "أؤكد عملية الخصم")]
    public bool ConfirmDeduction { get; set; }

    // Display-only, filled by the controller.
    [BindNever, ValidateNever] public bool IsDeduction { get; set; }
    [BindNever, ValidateNever] public string? StudentLabel { get; set; }
    [BindNever, ValidateNever] public int? StudentBalance { get; set; }
    [BindNever, ValidateNever] public List<ReasonOption> Reasons { get; set; } = new();
}

public class PointReasonListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DefaultPoints { get; set; }
    public bool IsDeduction { get; set; }
    public bool IsActive { get; set; }
}

public class PointReasonFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "يرجى إدخال اسم السبب.")]
    [StringLength(150, ErrorMessage = "الاسم لا يزيد عن 150 حرفًا.")]
    [Display(Name = "اسم السبب")]
    public string Name { get; set; } = string.Empty;

    [Range(1, 10000, ErrorMessage = "النقاط الافتراضية بين 1 و 10000.")]
    [Display(Name = "النقاط الافتراضية")]
    public int DefaultPoints { get; set; } = 5;

    [Display(Name = "سبب خصم (وليس إضافة)")]
    public bool IsDeduction { get; set; }
}
