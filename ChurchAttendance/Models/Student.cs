using System;
using System.Collections.Generic;

namespace ChurchAttendance.Models
{
    public class Student
    {
        public int Id { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? QRCodeValue { get; set; }

        // تعديل الأنواع لتطابق باقي خدمات النظام
        public DateOnly? DateOfBirth { get; set; }
        public Gender? Gender { get; set; }

        public string? Phone { get; set; }
        public string? ParentPhone { get; set; }

        // إضافة الحقول الناقصة لحل أخطاء GuardianName و GuardianPhone
        public string? GuardianName { get; set; }
        public string? GuardianPhone { get; set; }

        public string? Address { get; set; }
        public string? ProfileImagePath { get; set; }

        public int StageId { get; set; }
        public Stage Stage { get; set; } = null!;

        public int? GroupId { get; set; }
        public StudentGroup? Group { get; set; }

        public int PointsBalance { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
        public ICollection<PointTransaction> PointTransactions { get; set; } = new List<PointTransaction>();
        public ICollection<RewardRedemption> RewardRedemptions { get; set; } = new List<RewardRedemption>();
    }
}