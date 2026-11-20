using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

/// <summary>Named StudentGroup (table "Groups") to avoid clashing with System.Text.RegularExpressions.Group.</summary>
public class StudentGroup
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int StageId { get; set; }
    public Stage Stage { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Student> Students { get; set; } = new List<Student>();
}
