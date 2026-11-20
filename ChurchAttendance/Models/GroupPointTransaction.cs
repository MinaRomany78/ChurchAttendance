using System;

namespace ChurchAttendance.Models
{
    public class GroupPointTransaction
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public StudentGroup Group { get; set; } = null!;

        public int Points { get; set; }
        public string Reason { get; set; } = string.Empty;

        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;
        public ApplicationUser CreatedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }
    }
}