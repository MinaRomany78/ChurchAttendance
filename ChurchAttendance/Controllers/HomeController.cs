using ChurchAttendance.Data;
using ChurchAttendance.Security;
using ChurchAttendance.Models;
using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IAppClock _clock;
    private readonly IDashboardService _dashboard;
    private readonly ICatalogService _catalog;
    private readonly ICurrentStageAccessor _stage;

    public HomeController(ApplicationDbContext db, IAppClock clock, IDashboardService dashboard,
                          ICatalogService catalog, ICurrentStageAccessor stage)
    {
        _db = db;
        _clock = clock;
        _dashboard = dashboard;
        _catalog = catalog;
        _stage = stage;
    }

    [Authorize(Policy = AppPolicies.ViewData)]
    [RequireStageSelected]
    public async Task<IActionResult> Index([FromQuery] DashboardChartQuery chart)
    {
        var today = _clock.Today;

        var stageId = _stage.StageId;

        var todayRecords = _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.AttendanceSession.SessionDate == today && r.Student.StageId == stageId);

        var present = await todayRecords
            .Where(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late)
            .Select(r => r.StudentId).Distinct().CountAsync();

        var absent = await todayRecords
            .Where(r => r.Status == AttendanceStatus.Absent)
            .Select(r => r.StudentId).Distinct().CountAsync();

        var latestRaw = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.CheckInTime != null && r.Student.StageId == stageId)
            .OrderByDescending(r => r.CheckInTime)
            .Take(8)
            .Select(r => new
            {
                Name = r.Student.FullName,
                Code = r.Student.StudentCode,
                r.CheckInTime,
                r.Status,
                r.RegistrationMethod
            })
            .ToListAsync();

        // ---- weekly chart range (default: last 7 days, max 62 days)
        var chartTo = chart.To ?? today;
        var chartFrom = chart.From ?? chartTo.AddDays(-6);
        chart.StageId ??= stageId; // default the chart to the current stage; the dropdown can still widen it
        string? chartError = null;

        if (chartFrom > chartTo)
        {
            chartError = "تاريخ البداية يجب أن يكون قبل تاريخ النهاية. تم عرض آخر 7 أيام.";
            chartTo = today;
            chartFrom = today.AddDays(-6);
        }
        else if (chartTo.DayNumber - chartFrom.DayNumber > 61)
        {
            chartError = "الحد الأقصى للفترة 62 يومًا. تم تقصير الفترة.";
            chartFrom = chartTo.AddDays(-61);
        }

        var model = new DashboardViewModel
        {
            Today = today,
            ChartFrom = chartFrom,
            ChartTo = chartTo,
            ChartStageId = chart.StageId,
            ChartGroupId = chart.GroupId,
            ChartError = chartError,
            ChartDays = await _dashboard.GetAttendanceTrendAsync(chartFrom, chartTo, chart.StageId, chart.GroupId),
            Stages = await _catalog.GetStageOptionsAsync(includeInactive: true),
            Groups = await _catalog.GetGroupOptionsAsync(includeInactive: true),
            TotalStudents = await _db.Students.CountAsync(s => s.IsActive && s.StageId == stageId),
            PresentToday = present,
            AbsentToday = absent,
            TotalPoints = await _db.PointTransactions.Where(p => p.Student.StageId == stageId).SumAsync(p => (long?)p.Points) ?? 0,
            ActiveRewards = await _db.Rewards.CountAsync(r => r.IsActive),
            AttendanceRate = present + absent > 0 ? Math.Round(100.0 * present / (present + absent), 1) : 0,
            Latest = latestRaw.Select(x => new LatestAttendanceItem
            {
                StudentName = x.Name,
                StudentCode = x.Code,
                LocalTime = _clock.ToLocal(x.CheckInTime!.Value),
                Status = x.Status,
                Method = x.RegistrationMethod
            }).ToList()
        };

        return View(model);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? code)
    {
        var status = code ?? 500;

        var (title, message) = status switch
        {
            404 => ("الصفحة غير موجودة", "لم نتمكن من العثور على الصفحة المطلوبة. تأكد من الرابط أو عد إلى الصفحة الرئيسية."),
            403 => ("غير مسموح", "ليس لديك صلاحية للوصول إلى هذه الصفحة."),
            401 => ("تسجيل الدخول مطلوب", "يرجى تسجيل الدخول للمتابعة."),
            _ => ("حدث خطأ غير متوقع", "تعذر إكمال العملية. حاول مرة أخرى، وإذا استمرت المشكلة أبلغ مدير النظام.")
        };

        Response.StatusCode = status;
        return View(new ErrorViewModel { Code = status, Title = title, Message = message });
    }
}
