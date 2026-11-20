using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

public class AttendanceRecord
{
    public int Id { get; set; }

    public int AttendanceSessionId { get; set; }
    public AttendanceSession AttendanceSession { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public AttendanceStatus Status { get; set; }

    /// <summary>UTC. Null for Absent/Excused records that have no physical check-in.</summary>
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }

    public RegistrationMethod RegistrationMethod { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public string RegisteredByUserId { get; set; } = string.Empty;
    public ApplicationUser RegisteredBy { get; set; } = null!;
}
