using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

/// <summary>
/// UserId/UserName are stored as plain values (no foreign key) so the audit trail survives any user changes.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }

    [MaxLength(256)]
    public string? UserName { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? EntityId { get; set; }

    public string? OldValues { get; set; }
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(45)]
    public string? IpAddress { get; set; }
}
