using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

/// <summary>Administrator-configurable reasons for awarding or deducting points.</summary>
public class PointReason
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Suggested amount (always positive; direction comes from IsDeduction).</summary>
    public int DefaultPoints { get; set; }

    public bool IsDeduction { get; set; }
    public bool IsActive { get; set; } = true;
}
