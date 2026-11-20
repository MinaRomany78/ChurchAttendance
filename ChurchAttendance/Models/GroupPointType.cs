using System;

namespace ChurchAttendance.Models
{
    public class GroupPointType
    {
        public int Id { get; set; }
        public string NameArabic { get; set; } = string.Empty;
        public string? NameEnglish { get; set; }
        public int DefaultPoints { get; set; }
        public bool IsPositive { get; set; } = true;
        public bool AllowManualValueOverride { get; set; } = true;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}