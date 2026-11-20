using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ChurchAttendance.ViewModels;

public record StageOption(int Id, string Name);
public record StageSelectionItem(int Id, string Name, string? Description, bool RequiresCode);
public record GroupOption(int Id, string Name, int StageId);

public class StageListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public int GroupCount { get; set; }
    public int StudentCount { get; set; }
    public bool RequiresCode { get; set; }
}

public class StageAccessCodeViewModel
{
    public int Id { get; set; }
    public string StageName { get; set; } = string.Empty;
    public bool RequiresCode { get; set; }
    public string? Code { get; set; }
}

public class StageFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "يرجى إدخال اسم المرحلة.")]
    [StringLength(100, ErrorMessage = "اسم المرحلة لا يزيد عن 100 حرف.")]
    [Display(Name = "اسم المرحلة")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "الوصف لا يزيد عن 500 حرف.")]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Range(0, 1000, ErrorMessage = "الترتيب يجب أن يكون بين 0 و 1000.")]
    [Display(Name = "الترتيب")]
    public int SortOrder { get; set; }
}

public class GroupListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int StudentCount { get; set; }
}

public class GroupFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "يرجى إدخال اسم المجموعة.")]
    [StringLength(100, ErrorMessage = "اسم المجموعة لا يزيد عن 100 حرف.")]
    [Display(Name = "اسم المجموعة")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى اختيار المرحلة.")]
    [Display(Name = "المرحلة")]
    public int? StageId { get; set; }

    [StringLength(500, ErrorMessage = "الوصف لا يزيد عن 500 حرف.")]
    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [BindNever, ValidateNever]
    public List<StageOption> Stages { get; set; } = new();
}
