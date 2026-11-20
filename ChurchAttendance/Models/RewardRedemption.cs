using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

public class RewardRedemption
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public int RewardId { get; set; }
    public Reward Reward { get; set; } = null!;

    public int PointsUsed { get; set; }

    public RedemptionStatus RedemptionStatus { get; set; } = RedemptionStatus.Pending;

    public DateTime RedeemedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public string RequestedByUserId { get; set; } = string.Empty;
    public ApplicationUser RequestedBy { get; set; } = null!;

    public string? ApprovedByUserId { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}
