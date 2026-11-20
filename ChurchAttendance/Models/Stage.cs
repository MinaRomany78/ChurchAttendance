using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

public class Stage
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>PBKDF2 hash of the stage's access code, never the plaintext. Null/empty means no code is required.</summary>
    [MaxLength(250)]
    public string? AccessCodeHash { get; set; }

    public DateTime? AccessCodeUpdatedAt { get; set; }

    public ICollection<StudentGroup> Groups { get; set; } = new List<StudentGroup>();
    public ICollection<Student> Students { get; set; } = new List<Student>();
}
