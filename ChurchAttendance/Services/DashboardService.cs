using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class DashboardService : IDashboardService
{
    private readonly ApplicationDbContext _db;

    public DashboardService(ApplicationDbContext db) => _db = db;

    public async Task<List<ChartDay>> GetAttendanceTrendAsync(DateOnly from, DateOnly to, int? stageId, int? groupId)
    {
        IQueryable<AttendanceRecord> q = _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.AttendanceSession.SessionDate >= from && r.AttendanceSession.SessionDate <= to);

        if (stageId is int st) q = q.Where(r => r.Student.StageId == st);
        if (groupId is int gr) q = q.Where(r => r.Student.GroupId == gr);

        var byDay = await q
            .GroupBy(r => r.AttendanceSession.SessionDate)
            .Select(g => new
            {
                Date = g.Key,
                Present = g.Count(x => x.Status == AttendanceStatus.Present || x.Status == AttendanceStatus.Late),
                Absent = g.Count(x => x.Status == AttendanceStatus.Absent)
            })
            .ToListAsync();

        var days = new List<ChartDay>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var row = byDay.FirstOrDefault(x => x.Date == d);
            var total = row is null ? 0 : row.Present + row.Absent;

            days.Add(new ChartDay
            {
                Date = d,
                Present = row?.Present ?? 0,
                Absent = row?.Absent ?? 0,
                Percent = total == 0 ? (double?)null : Math.Round(100.0 * row!.Present / total, 1)
            });
        }

        return days;
    }
}
