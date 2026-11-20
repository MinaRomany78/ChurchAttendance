using ChurchAttendance.Data;
using ChurchAttendance.Helpers;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class ReportService : IReportService
{
    private static readonly ReportDefinition[] Defs =
    {
        new() { Key = "attendance", Title = "تقرير حضور الطلاب", Icon = "bi-person-check",
                Description = "كل تسجيلات الحضور خلال الفترة مع الحالة ووقت التسجيل وطريقته.",
                StatusMode = ReportStatusMode.AllStatuses },
        new() { Key = "absence", Title = "تقرير الغياب", Icon = "bi-person-x",
                Description = "الطلاب الغائبون (بعذر وبدون عذر) خلال الفترة.",
                StatusMode = ReportStatusMode.AbsenceOnly },
        new() { Key = "by-stage", Title = "تقرير الحضور حسب المرحلة", Icon = "bi-diagram-3",
                Description = "ملخص الحضور والغياب ونسبة الحضور لكل مرحلة.",
                ShowGroup = false, ShowStudent = false },
        new() { Key = "by-group", Title = "تقرير الحضور حسب المجموعة", Icon = "bi-collection",
                Description = "ملخص الحضور والغياب ونسبة الحضور لكل مجموعة.",
                ShowStudent = false },
        new() { Key = "points", Title = "تقرير نقاط الطلاب", Icon = "bi-star",
                Description = "النقاط المضافة والمخصومة والمستبدلة خلال الفترة، مع الرصيد الحالي لكل طالب." },
        new() { Key = "redemptions", Title = "تقرير المكافآت المستبدلة", Icon = "bi-gift",
                Description = "المكافآت التي استبدلها الطلاب خلال الفترة وحالة كل طلب." },
        new() { Key = "by-student", Title = "تقرير الطلاب حسب الحضور", Icon = "bi-graph-up",
                Description = "ترتيب الطلاب بحسب نسبة الحضور خلال الفترة." },
        new() { Key = "activity", Title = "تقرير الحركات خلال فترة زمنية", Icon = "bi-clock-history",
                Description = "سجل زمني موحّد لتسجيلات الحضور وحركات النقاط والاستبدالات." }
    };

    private readonly ApplicationDbContext _db;
    private readonly IAppClock _clock;

    public ReportService(ApplicationDbContext db, IAppClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public IReadOnlyList<ReportDefinition> Definitions => Defs;

    public ReportDefinition? Find(string? key) =>
        Defs.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));

    public Task<ReportResult> RunAsync(ReportDefinition def, ReportFilter f, int maxRows) => def.Key switch
    {
        "attendance" => AttendanceAsync(def, f, maxRows, absenceOnly: false),
        "absence" => AttendanceAsync(def, f, maxRows, absenceOnly: true),
        "by-stage" => ByStageAsync(def, f),
        "by-group" => ByGroupAsync(def, f),
        "points" => PointsAsync(def, f, maxRows),
        "redemptions" => RedemptionsAsync(def, f, maxRows),
        "by-student" => ByStudentAsync(def, f, maxRows),
        "activity" => ActivityAsync(def, f, maxRows),
        _ => throw new ArgumentException("Unknown report key.", nameof(def))
    };

    // =================================================================== 1 + 2: attendance / absence

    private async Task<ReportResult> AttendanceAsync(ReportDefinition def, ReportFilter f, int max, bool absenceOnly)
    {
        AttendanceStatus? status = f.Status;
        if (absenceOnly && status is not (AttendanceStatus.Absent or AttendanceStatus.Excused)) status = null;

        var q = AttendanceQuery(f, status);
        if (absenceOnly && status is null)
            q = q.Where(r => r.Status == AttendanceStatus.Absent || r.Status == AttendanceStatus.Excused);

        var counts = await q.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync();
        int C(AttendanceStatus s) => counts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var raw = await q
            .OrderByDescending(r => r.AttendanceSession.SessionDate).ThenBy(r => r.Student.FullName)
            .Take(max + 1)
            .Select(r => new
            {
                Date = r.AttendanceSession.SessionDate,
                r.AttendanceSession.Title,
                r.AttendanceSession.SessionType,
                Name = r.Student.FullName,
                Code = r.Student.StudentCode,
                Stage = r.Student.Stage.Name,
                Group = r.Student.Group != null ? r.Student.Group.Name : null,
                r.Status,
                r.CheckInTime,
                r.RegistrationMethod,
                r.Notes
            })
            .ToListAsync();

        var result = await NewResultAsync(def, f, max);
        result.Truncated = raw.Count > max;
        var rows = raw.Take(max).ToList();

        if (absenceOnly)
        {
            result.Columns = Cols("التاريخ:D", "الجلسة", "الطالب", "الكود", "المرحلة", "المجموعة", "الحالة", "ملاحظات");
            result.Rows = rows.Select(r => new object?[]
            {
                r.Date.ToDateTime(TimeOnly.MinValue), r.Title ?? ArabicText.SessionTypeText(r.SessionType),
                r.Name, r.Code, r.Stage, r.Group ?? "—", ArabicText.Status(r.Status), r.Notes ?? ""
            }).ToList();

            var absentStudents = await q.Select(r => r.StudentId).Distinct().CountAsync();
            result.Summary = new()
            {
                new("إجمالي سجلات الغياب", (C(AttendanceStatus.Absent) + C(AttendanceStatus.Excused)).ToString("N0")),
                new("غياب بدون عذر", C(AttendanceStatus.Absent).ToString("N0")),
                new("غياب بعذر", C(AttendanceStatus.Excused).ToString("N0")),
                new("عدد الطلاب الغائبين", absentStudents.ToString("N0"))
            };
        }
        else
        {
            result.Columns = Cols("التاريخ:D", "الجلسة", "الطالب", "الكود", "المرحلة", "المجموعة", "الحالة", "وقت التسجيل", "طريقة التسجيل");
            result.Rows = rows.Select(r => new object?[]
            {
                r.Date.ToDateTime(TimeOnly.MinValue), r.Title ?? ArabicText.SessionTypeText(r.SessionType),
                r.Name, r.Code, r.Stage, r.Group ?? "—", ArabicText.Status(r.Status),
                r.CheckInTime is null ? "—" : _clock.ToLocal(r.CheckInTime.Value).ToString("hh:mm tt"),
                ArabicText.Method(r.RegistrationMethod)
            }).ToList();

            var total = counts.Sum(x => x.Count);
            result.Summary = new()
            {
                new("إجمالي السجلات", total.ToString("N0")),
                new("حاضر", C(AttendanceStatus.Present).ToString("N0")),
                new("متأخر", C(AttendanceStatus.Late).ToString("N0")),
                new("غائب", C(AttendanceStatus.Absent).ToString("N0")),
                new("غائب بعذر", C(AttendanceStatus.Excused).ToString("N0")),
                new("نسبة الحضور", Pct(Rate(C(AttendanceStatus.Present), C(AttendanceStatus.Late), C(AttendanceStatus.Absent))))
            };
        }

        return result;
    }

    // =================================================================== 3: by stage

    private async Task<ReportResult> ByStageAsync(ReportDefinition def, ReportFilter f)
    {
        var raw = await AttendanceQuery(f, null)
            .GroupBy(r => new { r.Student.StageId, StageName = r.Student.Stage.Name, StageOrder = r.Student.Stage.SortOrder })
            .Select(g => new
            {
                g.Key.StageName,
                g.Key.StageOrder,
                Sessions = g.Select(x => x.AttendanceSessionId).Distinct().Count(),
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present),
                Late = g.Count(x => x.Status == AttendanceStatus.Late),
                Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
            })
            .OrderBy(x => x.StageOrder).ThenBy(x => x.StageName)
            .ToListAsync();

        var result = await NewResultAsync(def, f, int.MaxValue);
        result.Columns = Cols("المرحلة", "عدد الجلسات:N", "إجمالي السجلات:N", "حاضر:N", "متأخر:N", "غائب:N", "غائب بعذر:N", "نسبة الحضور:P");
        result.Rows = raw.Select(r => new object?[]
        {
            r.StageName, r.Sessions, r.Total, r.Present, r.Late, r.Absent, r.Excused, Rate(r.Present, r.Late, r.Absent)
        }).ToList();

        AddAttendanceTotals(result, raw.Sum(x => x.Total), raw.Sum(x => x.Present), raw.Sum(x => x.Late),
            raw.Sum(x => x.Absent), raw.Sum(x => x.Excused));
        return result;
    }

    // =================================================================== 4: by group

    private async Task<ReportResult> ByGroupAsync(ReportDefinition def, ReportFilter f)
    {
        var raw = await AttendanceQuery(f, null)
            .GroupBy(r => new
            {
                StageName = r.Student.Stage.Name,
                StageOrder = r.Student.Stage.SortOrder,
                GroupName = r.Student.Group != null ? r.Student.Group.Name : "بدون مجموعة"
            })
            .Select(g => new
            {
                g.Key.StageName,
                g.Key.StageOrder,
                g.Key.GroupName,
                Sessions = g.Select(x => x.AttendanceSessionId).Distinct().Count(),
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present),
                Late = g.Count(x => x.Status == AttendanceStatus.Late),
                Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
            })
            .OrderBy(x => x.StageOrder).ThenBy(x => x.GroupName)
            .ToListAsync();

        var result = await NewResultAsync(def, f, int.MaxValue);
        result.Columns = Cols("المرحلة", "المجموعة", "عدد الجلسات:N", "إجمالي السجلات:N", "حاضر:N", "متأخر:N", "غائب:N", "غائب بعذر:N", "نسبة الحضور:P");
        result.Rows = raw.Select(r => new object?[]
        {
            r.StageName, r.GroupName, r.Sessions, r.Total, r.Present, r.Late, r.Absent, r.Excused, Rate(r.Present, r.Late, r.Absent)
        }).ToList();

        AddAttendanceTotals(result, raw.Sum(x => x.Total), raw.Sum(x => x.Present), raw.Sum(x => x.Late),
            raw.Sum(x => x.Absent), raw.Sum(x => x.Excused));
        return result;
    }

    private static void AddAttendanceTotals(ReportResult result, int total, int present, int late, int absent, int excused)
    {
        result.Summary = new()
        {
            new("إجمالي السجلات", total.ToString("N0")),
            new("حاضر", present.ToString("N0")),
            new("متأخر", late.ToString("N0")),
            new("غائب", absent.ToString("N0")),
            new("غائب بعذر", excused.ToString("N0")),
            new("نسبة الحضور", Pct(Rate(present, late, absent)))
        };
    }

    // =================================================================== 5: points

    private async Task<ReportResult> PointsAsync(ReportDefinition def, ReportFilter f, int max)
    {
        var (start, end) = UtcRange(f);

        var raw = await StudentScope(f).Where(s => s.IsActive)
            .OrderBy(s => s.FullName)
            .Select(s => new
            {
                s.StudentCode,
                s.FullName,
                Stage = s.Stage.Name,
                Group = s.Group != null ? s.Group.Name : null,
                Earned = s.PointTransactions
                    .Where(t => t.Points > 0 && t.TransactionType != PointTransactionType.Refund
                                && (start == null || t.CreatedAt >= start) && (end == null || t.CreatedAt < end))
                    .Sum(t => (int?)t.Points) ?? 0,
                Deducted = s.PointTransactions
                    .Where(t => t.Points < 0 && t.TransactionType != PointTransactionType.Redemption
                                && (start == null || t.CreatedAt >= start) && (end == null || t.CreatedAt < end))
                    .Sum(t => (int?)(-t.Points)) ?? 0,
                // Net of refunds, so a cancelled redemption cancels itself out.
                Redeemed = s.PointTransactions
                    .Where(t => (t.TransactionType == PointTransactionType.Redemption || t.TransactionType == PointTransactionType.Refund)
                                && (start == null || t.CreatedAt >= start) && (end == null || t.CreatedAt < end))
                    .Sum(t => (int?)(-t.Points)) ?? 0,
                Balance = s.PointTransactions.Sum(t => (int?)t.Points) ?? 0
            })
            .ToListAsync();

        var result = await NewResultAsync(def, f, max);
        result.Truncated = raw.Count > max;
        result.Columns = Cols("كود الطالب", "الطالب", "المرحلة", "المجموعة", "المضاف:N", "المخصوم:N", "المستبدل (صافي):N", "الرصيد الحالي:N");
        result.Rows = raw.Take(max).Select(r => new object?[]
        {
            r.StudentCode, r.FullName, r.Stage, r.Group ?? "—", r.Earned, r.Deducted, r.Redeemed, r.Balance
        }).ToList();

        result.Summary = new()
        {
            new("عدد الطلاب", raw.Count.ToString("N0")),
            new("إجمالي المضاف", raw.Sum(x => x.Earned).ToString("N0")),
            new("إجمالي المخصوم", raw.Sum(x => x.Deducted).ToString("N0")),
            new("إجمالي المستبدل", raw.Sum(x => x.Redeemed).ToString("N0")),
            new("مجموع الأرصدة الحالية", raw.Sum(x => (long)x.Balance).ToString("N0"))
        };
        return result;
    }

    // =================================================================== 6: redemptions

    private async Task<ReportResult> RedemptionsAsync(ReportDefinition def, ReportFilter f, int max)
    {
        var q = RedemptionQuery(f);

        var stats = await q.GroupBy(r => r.RedemptionStatus)
            .Select(g => new { Status = g.Key, Count = g.Count(), Points = g.Sum(x => x.PointsUsed) })
            .ToListAsync();

        var raw = await q
            .OrderByDescending(r => r.RedeemedAt).ThenByDescending(r => r.Id)
            .Take(max + 1)
            .Select(r => new
            {
                r.RedeemedAt,
                Name = r.Student.FullName,
                Code = r.Student.StudentCode,
                Stage = r.Student.Stage.Name,
                Reward = r.Reward.Name,
                r.PointsUsed,
                r.RedemptionStatus,
                r.Notes
            })
            .ToListAsync();

        var result = await NewResultAsync(def, f, max);
        result.Truncated = raw.Count > max;
        result.Columns = Cols("التاريخ:T", "الطالب", "الكود", "المرحلة", "المكافأة", "النقاط:N", "الحالة", "ملاحظات");
        result.Rows = raw.Take(max).Select(r => new object?[]
        {
            _clock.ToLocal(r.RedeemedAt), r.Name, r.Code, r.Stage, r.Reward, r.PointsUsed,
            ArabicText.RedemptionStatusText(r.RedemptionStatus), r.Notes ?? ""
        }).ToList();

        var active = stats.Where(s => s.Status != RedemptionStatus.Cancelled).ToList();
        result.Summary = new()
        {
            new("عدد الطلبات", stats.Sum(s => s.Count).ToString("N0")),
            new("تم تسليمها", (stats.FirstOrDefault(s => s.Status == RedemptionStatus.Delivered)?.Count ?? 0).ToString("N0")),
            new("ملغاة", (stats.FirstOrDefault(s => s.Status == RedemptionStatus.Cancelled)?.Count ?? 0).ToString("N0")),
            new("النقاط المستخدمة (بدون الملغاة)", active.Sum(s => (long)s.Points).ToString("N0"))
        };
        return result;
    }

    // =================================================================== 7: students by attendance

    private async Task<ReportResult> ByStudentAsync(ReportDefinition def, ReportFilter f, int max)
    {
        var raw = await AttendanceQuery(f, null)
            .GroupBy(r => new
            {
                r.StudentId,
                Name = r.Student.FullName,
                Code = r.Student.StudentCode,
                Stage = r.Student.Stage.Name,
                GroupName = r.Student.Group != null ? r.Student.Group.Name : "—"
            })
            .Select(g => new
            {
                g.Key.Name,
                g.Key.Code,
                g.Key.Stage,
                g.Key.GroupName,
                Total = g.Count(),
                Present = g.Count(x => x.Status == AttendanceStatus.Present),
                Late = g.Count(x => x.Status == AttendanceStatus.Late),
                Absent = g.Count(x => x.Status == AttendanceStatus.Absent),
                Excused = g.Count(x => x.Status == AttendanceStatus.Excused)
            })
            .ToListAsync();

        var ranked = raw
            .Select(r => new { r, Rate = Rate(r.Present, r.Late, r.Absent) })
            .OrderByDescending(x => x.Rate).ThenByDescending(x => x.r.Present + x.r.Late).ThenBy(x => x.r.Name)
            .ToList();

        var result = await NewResultAsync(def, f, max);
        result.Truncated = ranked.Count > max;
        result.Columns = Cols("#:N", "الطالب", "الكود", "المرحلة", "المجموعة", "إجمالي السجلات:N", "حاضر:N", "متأخر:N", "غائب:N", "غائب بعذر:N", "نسبة الحضور:P");

        var rank = 0;
        result.Rows = ranked.Take(max).Select(x => new object?[]
        {
            ++rank, x.r.Name, x.r.Code, x.r.Stage, x.r.GroupName, x.r.Total, x.r.Present, x.r.Late, x.r.Absent, x.r.Excused, x.Rate
        }).ToList();

        result.Summary = new()
        {
            new("عدد الطلاب", ranked.Count.ToString("N0")),
            new("متوسط نسبة الحضور", Pct(ranked.Count == 0 ? 0 : Math.Round(ranked.Average(x => x.Rate), 1))),
            new("بنسبة حضور 100%", ranked.Count(x => x.Rate >= 100).ToString("N0")),
            new("بنسبة حضور أقل من 50%", ranked.Count(x => x.Rate < 50).ToString("N0"))
        };
        return result;
    }

    // =================================================================== 8: activity feed

    private async Task<ReportResult> ActivityAsync(ReportDefinition def, ReportFilter f, int max)
    {
        var (start, end) = UtcRange(f);
        var hasStudentFilter = HasStudentFilter(f);
        var studentIds = StudentScope(f).Select(s => s.Id);

        var attendance = await AttendanceQuery(f, null)
            .Where(r => r.CheckInTime != null)
            .OrderByDescending(r => r.CheckInTime).Take(max + 1)
            .Select(r => new { When = r.CheckInTime!.Value, Name = r.Student.FullName, Code = r.Student.StudentCode, r.Status, r.RegistrationMethod, r.AttendanceSession.Title, r.AttendanceSession.SessionType })
            .ToListAsync();

        var pointsQuery = _db.PointTransactions.AsNoTracking().AsQueryable();
        if (start is DateTime s0) pointsQuery = pointsQuery.Where(t => t.CreatedAt >= s0);
        if (end is DateTime e0) pointsQuery = pointsQuery.Where(t => t.CreatedAt < e0);
        if (hasStudentFilter) pointsQuery = pointsQuery.Where(t => studentIds.Contains(t.StudentId));

        var points = await pointsQuery
            .OrderByDescending(t => t.CreatedAt).Take(max + 1)
            .Select(t => new { When = t.CreatedAt, Name = t.Student.FullName, Code = t.Student.StudentCode, t.TransactionType, t.Points, t.Reason })
            .ToListAsync();

        var redemptions = await RedemptionQuery(f)
            .OrderByDescending(r => r.RedeemedAt).Take(max + 1)
            .Select(r => new { When = r.RedeemedAt, Name = r.Student.FullName, Code = r.Student.StudentCode, Reward = r.Reward.Name, r.PointsUsed, r.RedemptionStatus })
            .ToListAsync();

        var events = new List<(DateTime When, string Type, string Name, string Code, string Details, string Value)>();

        events.AddRange(attendance.Select(a => (_clock.ToLocal(a.When), "حضور", a.Name, a.Code,
            $"{a.Title ?? ArabicText.SessionTypeText(a.SessionType)} — {ArabicText.Method(a.RegistrationMethod)}",
            ArabicText.Status(a.Status))));

        events.AddRange(points.Select(p => (_clock.ToLocal(p.When), "نقاط", p.Name, p.Code,
            $"{ArabicText.TransactionTypeText(p.TransactionType)} — {p.Reason}",
            (p.Points > 0 ? "+" : "") + p.Points)));

        events.AddRange(redemptions.Select(r => (_clock.ToLocal(r.When), "استبدال", r.Name, r.Code,
            r.Reward, $"{r.PointsUsed:N0} نقطة — {ArabicText.RedemptionStatusText(r.RedemptionStatus)}")));

        var ordered = events.OrderByDescending(e => e.When).ToList();

        var result = await NewResultAsync(def, f, max);
        result.Truncated = ordered.Count > max || attendance.Count > max || points.Count > max || redemptions.Count > max;
        var shown = ordered.Take(max).ToList();

        result.Columns = Cols("التاريخ والوقت:T", "النوع", "الطالب", "الكود", "التفاصيل", "النتيجة / القيمة");
        result.Rows = shown.Select(e => new object?[] { e.When, e.Type, e.Name, e.Code, e.Details, e.Value }).ToList();

        result.Summary = new()
        {
            new("إجمالي الحركات المعروضة", shown.Count.ToString("N0")),
            new("تسجيلات حضور", shown.Count(e => e.Type == "حضور").ToString("N0")),
            new("حركات نقاط", shown.Count(e => e.Type == "نقاط").ToString("N0")),
            new("استبدالات", shown.Count(e => e.Type == "استبدال").ToString("N0"))
        };
        return result;
    }

    // =================================================================== shared query helpers

    private static bool HasStudentFilter(ReportFilter f) =>
        f.StageId is not null || f.GroupId is not null || !string.IsNullOrWhiteSpace(f.Student);

    private IQueryable<Student> StudentScope(ReportFilter f)
    {
        IQueryable<Student> q = _db.Students.AsNoTracking();

        if (f.StageId is int stageId) q = q.Where(s => s.StageId == stageId);
        if (f.GroupId is int groupId) q = q.Where(s => s.GroupId == groupId);

        if (!string.IsNullOrWhiteSpace(f.Student))
        {
            var term = f.Student.Trim();
            q = q.Where(s => s.FullName.Contains(term) || s.StudentCode.Contains(term));
        }

        return q;
    }

    private IQueryable<AttendanceRecord> AttendanceQuery(ReportFilter f, AttendanceStatus? status)
    {
        IQueryable<AttendanceRecord> q = _db.AttendanceRecords.AsNoTracking();

        if (f.From is DateOnly fromDate) q = q.Where(r => r.AttendanceSession.SessionDate >= fromDate);
        if (f.To is DateOnly toDate) q = q.Where(r => r.AttendanceSession.SessionDate <= toDate);
        if (f.StageId is int stageId) q = q.Where(r => r.Student.StageId == stageId);
        if (f.GroupId is int groupId) q = q.Where(r => r.Student.GroupId == groupId);

        if (!string.IsNullOrWhiteSpace(f.Student))
        {
            var term = f.Student.Trim();
            q = q.Where(r => r.Student.FullName.Contains(term) || r.Student.StudentCode.Contains(term));
        }

        if (status is AttendanceStatus st) q = q.Where(r => r.Status == st);
        return q;
    }

    private IQueryable<RewardRedemption> RedemptionQuery(ReportFilter f)
    {
        IQueryable<RewardRedemption> q = _db.RewardRedemptions.AsNoTracking();
        var (start, end) = UtcRange(f);

        if (start is DateTime s0) q = q.Where(r => r.RedeemedAt >= s0);
        if (end is DateTime e0) q = q.Where(r => r.RedeemedAt < e0);

        if (HasStudentFilter(f))
        {
            var ids = StudentScope(f).Select(s => s.Id);
            q = q.Where(r => ids.Contains(r.StudentId));
        }

        return q;
    }

    /// <summary>Local calendar dates -> UTC range [start, end).</summary>
    private (DateTime? Start, DateTime? End) UtcRange(ReportFilter f) =>
        (f.From is DateOnly a ? (DateTime?)_clock.StartOfDayUtc(a) : null,
         f.To is DateOnly b ? (DateTime?)_clock.StartOfDayUtc(b.AddDays(1)) : null);

    // =================================================================== small helpers

    private async Task<ReportResult> NewResultAsync(ReportDefinition def, ReportFilter f, int max) => new()
    {
        Title = def.Title,
        Description = def.Description,
        FiltersText = await DescribeAsync(f, def),
        GeneratedAtLocal = _clock.ToLocal(_clock.UtcNow),
        MaxRows = max
    };

    private async Task<string> DescribeAsync(ReportFilter f, ReportDefinition def)
    {
        var parts = new List<string>();

        parts.Add(f.From is null && f.To is null
            ? "كل الفترات"
            : $"الفترة: {f.From?.ToString("yyyy/MM/dd") ?? "…"} إلى {f.To?.ToString("yyyy/MM/dd") ?? "…"}");

        if (f.StageId is int stageId)
        {
            var name = await _db.Stages.Where(s => s.Id == stageId).Select(s => s.Name).FirstOrDefaultAsync();
            if (name is not null) parts.Add($"المرحلة: {name}");
        }

        if (def.ShowGroup && f.GroupId is int groupId)
        {
            var name = await _db.Groups.Where(g => g.Id == groupId).Select(g => g.Name).FirstOrDefaultAsync();
            if (name is not null) parts.Add($"المجموعة: {name}");
        }

        if (def.ShowStudent && !string.IsNullOrWhiteSpace(f.Student)) parts.Add($"الطالب: {f.Student.Trim()}");

        if (def.StatusMode != ReportStatusMode.None && f.Status is AttendanceStatus status)
            parts.Add($"الحالة: {ArabicText.Status(status)}");

        return string.Join(" — ", parts);
    }

    /// <summary>
    /// "Header" or "Header:X" where X = N (number), P (percent), D (date), T (date+time).
    /// </summary>
    private static List<ReportColumn> Cols(params string[] specs) => specs.Select(spec =>
    {
        var i = spec.LastIndexOf(':');
        if (i < 0) return new ReportColumn(spec);

        var kind = spec[(i + 1)..] switch
        {
            "N" => ReportColumnKind.Number,
            "P" => ReportColumnKind.Percent,
            "D" => ReportColumnKind.Date,
            "T" => ReportColumnKind.DateTime,
            _ => ReportColumnKind.Text
        };
        return new ReportColumn(spec[..i], kind);
    }).ToList();

    /// <summary>(Present + Late) / (Present + Late + Absent) as a percentage. Excused is excluded from the base.</summary>
    private static double Rate(int present, int late, int absent)
    {
        var baseCount = present + late + absent;
        return baseCount == 0 ? 0 : Math.Round(100.0 * (present + late) / baseCount, 1);
    }

    private static string Pct(double value) => value.ToString("0.#") + "%";
}
