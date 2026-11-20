using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

public class PointTransaction
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public PointTransactionType TransactionType { get; set; }

    /// <summary>SIGNED, never zero. Positive adds, negative subtracts.</summary>
    public int Points { get; set; }

    [Required, MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    public int? PointReasonId { get; set; }
    public PointReason? PointReason { get; set; }

    [MaxLength(50)]
    public string? ReferenceType { get; set; }
    public int? ReferenceId { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }
}
