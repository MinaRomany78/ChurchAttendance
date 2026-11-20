using ChurchAttendance.Models;

namespace ChurchAttendance.ViewModels;

public class DashboardViewModel
{
    public DateOnly Today { get; set; }

    public int TotalStudents { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public long TotalPoints { get; set; }
    public int ActiveRewards { get; set; }

    /// <summary>Percentage of (present + absent) recorded today. Excused students are excluded from the base.</summary>
    public double AttendanceRate { get; set; }

    public List<LatestAttendanceItem> Latest { get; set; } = new();

    // Weekly attendance chart
    public DateOnly ChartFrom { get; set; }
    public DateOnly ChartTo { get; set; }
    public int? ChartStageId { get; set; }
    public int? ChartGroupId { get; set; }
    public string? ChartError { get; set; }
    public List<ChartDay> ChartDays { get; set; } = new();
    public List<StageOption> Stages { get; set; } = new();
    public List<GroupOption> Groups { get; set; } = new();
}

public class LatestAttendanceItem
{
    public string StudentName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public DateTime LocalTime { get; set; }
    public AttendanceStatus Status { get; set; }
    public RegistrationMethod Method { get; set; }
}
