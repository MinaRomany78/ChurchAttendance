using System.ComponentModel.DataAnnotations;

namespace ChurchAttendance.Models;

public class Reward
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public int RequiredPoints { get; set; }

    [MaxLength(300)]
    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Null means unlimited stock.</summary>
    public int? StockQuantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Optimistic concurrency token so two simultaneous redemptions cannot oversell stock.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
