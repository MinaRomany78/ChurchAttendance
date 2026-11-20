using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

public class AttendanceSession
{
    public int Id { get; set; }

    [MaxLength(150)]
    public string? Title { get; set; }

    /// <summary>Local (church time zone) calendar date of the session.</summary>
    public DateOnly SessionDate { get; set; }

    /// <summary>Local start time.</summary>
    public TimeOnly StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }

    public SessionType SessionType { get; set; } = SessionType.Regular;

    public int? StageId { get; set; }
    public Stage? Stage { get; set; }

    public int? GroupId { get; set; }
    public StudentGroup? Group { get; set; }

    public bool IsClosed { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedBy { get; set; } = null!;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}
