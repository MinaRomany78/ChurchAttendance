using System.ComponentModel.DataAnnotations;
using ChurchAttendance.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ChurchAttendance.ViewModels;

public record RewardOption(int Id, string Name, int RequiredPoints, int? StockQuantity);

public class RewardListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int RequiredPoints { get; set; }
    public int? StockQuantity { get; set; }
    public bool IsActive { get; set; }
    public int RedemptionCount { get; set; }
}

public class RewardIndexViewModel
{
    public List<RewardListItem> Rewards { get; set; } = new();
    public bool CanManage { get; set; }
    public bool CanRedeem { get; set; }
}

public class RewardFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "يرجى إدخال اسم المكافأة.")]
    [StringLength(150, ErrorMessage = "الاسم لا يزيد عن 150 حرفًا.")]
    [Display(Name = "اسم المكافأة")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "الوصف لا يزيد عن 1000 حرف.")]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "يرجى إدخال النقاط المطلوبة.")]
    [Range(1, 100000, ErrorMessage = "النقاط المطلوبة بين 1 و 100000.")]
    [Display(Name = "النقاط المطلوبة")]
    public int? RequiredPoints { get; set; }

    [Range(0, 100000, ErrorMessage = "الكمية بين 0 و 100000.")]
    [Display(Name = "الكمية المتاحة (اتركها فارغة لكمية غير محدودة)")]
    public int? StockQuantity { get; set; }
}

public class RedeemFormViewModel
{
    [Required(ErrorMessage = "يرجى اختيار الطالب.")]
    public int? StudentId { get; set; }

    [Required(ErrorMessage = "يرجى اختيار المكافأة.")]
    [Display(Name = "المكافأة")]
    public int? RewardId { get; set; }

    [StringLength(500, ErrorMessage = "الملاحظات لا تزيد عن 500 حرف.")]
    [Display(Name = "ملاحظات (اختياري)")]
    public string? Notes { get; set; }

    [BindNever, ValidateNever] public string? StudentLabel { get; set; }
    [BindNever, ValidateNever] public int? StudentBalance { get; set; }
    [BindNever, ValidateNever] public List<RewardOption> Rewards { get; set; } = new();
}

public class RedeemPreviewViewModel
{
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public int Balance { get; set; }
    public int RewardId { get; set; }
    public string RewardName { get; set; } = string.Empty;
    public int RequiredPoints { get; set; }
    public int BalanceAfter => Balance - RequiredPoints;
    public string? Notes { get; set; }
}

public class RedemptionListQuery
{
    public string? Search { get; set; }
    public RedemptionStatus? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public class RedemptionListItem
{
    public int Id { get; set; }
    public DateTime LocalDate { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string RewardName { get; set; } = string.Empty;
    public int PointsUsed { get; set; }
    public RedemptionStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class RedemptionIndexViewModel
{
    public RedemptionListQuery Query { get; set; } = new();
    public PagedResult<RedemptionListItem> Result { get; set; } = new();
    public bool CanManage { get; set; }
}
