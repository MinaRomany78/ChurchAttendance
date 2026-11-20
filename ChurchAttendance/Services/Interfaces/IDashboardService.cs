using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IDashboardService
{
    /// <summary>
    /// One entry per calendar day in [from, to]. Percent is null on days without attendance data, so the chart
    /// shows a gap instead of a misleading 0%.
    /// </summary>
    Task<List<ChartDay>> GetAttendanceTrendAsync(DateOnly from, DateOnly to, int? stageId, int? groupId);
}
