using ChurchAttendance.Data;
using ChurchAttendance.Helpers;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class AttendanceService : IAttendanceService
{
    private const int PageSize = 15;
    private const int DefaultLateAfterMinutes = 15;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAppClock _clock;

    public AttendanceService(ApplicationDbContext db, IAuditService audit, IAppClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    // ------------------------------------------------------------------ sessions: list / details

    public async Task<PagedResult<SessionListItem>> SearchSessionsAsync(SessionListQuery q)
    {
        IQueryable<AttendanceSession> query = _db.AttendanceSessions.AsNoTracking();

        if (q.From is DateOnly fromDate) query = query.Where(s => s.SessionDate >= fromDate);
        if (q.To is DateOnly toDate) query = query.Where(s => s.SessionDate <= toDate);
        if (q.StageId is int stageId) query = query.Where(s => s.StageId == stageId);

        query = q.Status switch
        {
            "open" => query.Where(s => !s.IsClosed),
            "closed" => query.Where(s => s.IsClosed),
            _ => query
        };

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(q.Page, 1, totalPages);

        var items = await query
            .OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.StartTime).ThenByDescending(s => s.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(s => new SessionListItem
            {
                Id = s.Id,
                Title = s.Title,
                Date = s.SessionDate,
                StartTime = s.StartTime,
                Type = s.SessionType,
                StageName = s.Stage != null ? s.Stage.Name : null,
                GroupName = s.Group != null ? s.Group.Name : null,
                IsClosed = s.IsClosed,
                PresentCount = s.Records.Count(r => r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late),
                AbsentCount = s.Records.Count(r => r.Status == AttendanceStatus.Absent),
                TotalRecords = s.Records.Count
            })
            .ToListAsync();

        return new PagedResult<SessionListItem> { Items = items, Page = page, PageSize = PageSize, TotalCount = total };
    }

    public async Task<SessionDetailsViewModel?> GetSessionAsync(int id)
    {
        var s = await _db.AttendanceSessions.AsNoTracking()
            .Include(x => x.Stage).Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;

        var rowsRaw = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.AttendanceSessionId == id)
            .OrderBy(r => r.CheckInTime == null).ThenByDescending(r => r.CheckInTime).ThenBy(r => r.Student.FullName)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                Name = r.Student.FullName,
                Code = r.Student.StudentCode,
                StageName = r.Student.Stage.Name,
                r.Status,
                r.CheckInTime,
                r.RegistrationMethod,
                r.Notes
            })
            .ToListAsync();

        int Count(AttendanceStatus st) => rowsRaw.Count(r => r.Status == st);

        var notRegistered = await ScopeQuery(s.StageId, s.GroupId)
            .CountAsync(st => !st.AttendanceRecords.Any(r => r.AttendanceSessionId == id));

        return new SessionDetailsViewModel
        {
            Id = s.Id,
            StageId = s.StageId,
            Title = s.Title,
            Date = s.SessionDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Type = s.SessionType,
            StageName = s.Stage?.Name,
            GroupName = s.Group?.Name,
            IsClosed = s.IsClosed,
            PresentCount = Count(AttendanceStatus.Present),
            LateCount = Count(AttendanceStatus.Late),
            AbsentCount = Count(AttendanceStatus.Absent),
            ExcusedCount = Count(AttendanceStatus.Excused),
            NotRegisteredCount = notRegistered,
            AllowExcused = await AllowExcusedAsync(),
            Rows = rowsRaw.Select(r => new AttendanceRow
            {
                RecordId = r.Id,
                StudentId = r.StudentId,
                StudentName = r.Name,
                StudentCode = r.Code,
                StageName = r.StageName,
                Status = r.Status,
                LocalCheckIn = r.CheckInTime is null ? null : _clock.ToLocal(r.CheckInTime.Value),
                Method = r.RegistrationMethod,
                Notes = r.Notes
            }).ToList()
        };
    }

    // ------------------------------------------------------------------ sessions: create / close / reopen

    public async Task<ServiceResult<int>> CreateSessionAsync(SessionFormViewModel m, string userId)
    {
        var error = await ValidateSessionAsync(m);
        if (error is not null) return ServiceResult<int>.Fail(error);

        var date = m.SessionDate!.Value;

        var duplicateOpen = await _db.AttendanceSessions.AnyAsync(s =>
            !s.IsClosed && s.SessionDate == date && s.SessionType == m.SessionType
            && s.StageId == m.StageId && s.GroupId == m.GroupId);
        if (duplicateOpen)
            return ServiceResult<int>.Fail("توجد جلسة مفتوحة مماثلة بنفس التاريخ والنوع والنطاق. استخدمها أو أغلقها أولًا.");

        var session = new AttendanceSession
        {
            Title = string.IsNullOrWhiteSpace(m.Title) ? null : m.Title.Trim(),
            SessionDate = date,
            StartTime = m.StartTime!.Value,
            SessionType = m.SessionType,
            StageId = m.StageId,
            GroupId = m.GroupId,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow
        };

        _db.AttendanceSessions.Add(session);
        await _db.SaveChangesAsync();

        _audit.Add("AttendanceSessionOpened", "AttendanceSession", session.Id.ToString(), null,
            new { session.SessionDate, session.SessionType, session.StageId, session.GroupId });
        await _db.SaveChangesAsync();

        return ServiceResult<int>.Ok(session.Id);
    }

    /// <summary>Closing marks every in-scope active student without a record as Absent, in one transaction.</summary>
    public async Task<ServiceResult> CloseSessionAsync(int id, string userId)
    {
        var session = await _db.AttendanceSessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null) return ServiceResult.Fail("الجلسة غير موجودة.");
        if (session.IsClosed) return ServiceResult.Fail("الجلسة مغلقة بالفعل.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var registered = _db.AttendanceRecords.Where(r => r.AttendanceSessionId == id).Select(r => r.StudentId);
        var missingIds = await ScopeQuery(session.StageId, session.GroupId)
            .Where(s => !registered.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        foreach (var studentId in missingIds)
        {
            _db.AttendanceRecords.Add(new AttendanceRecord
            {
                AttendanceSessionId = id,
                StudentId = studentId,
                Status = AttendanceStatus.Absent,
                RegistrationMethod = RegistrationMethod.Manual,
                RegisteredByUserId = userId,
                Notes = "غياب تلقائي عند إغلاق الجلسة"
            });
        }

        session.IsClosed = true;
        session.EndTime ??= TimeOnly.FromDateTime(_clock.ToLocal(_clock.UtcNow));

        _audit.Add("AttendanceSessionClosed", "AttendanceSession", id.ToString(), null,
            new { autoAbsent = missingIds.Count });

        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ReopenSessionAsync(int id)
    {
        var session = await _db.AttendanceSessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session is null) return ServiceResult.Fail("الجلسة غير موجودة.");
        if (!session.IsClosed) return ServiceResult.Fail("الجلسة مفتوحة بالفعل.");

        session.IsClosed = false;
        _audit.Add("AttendanceSessionReopened", "AttendanceSession", id.ToString());
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // ------------------------------------------------------------------ registration

    public async Task<RegisterOutcome> RegisterAsync(int sessionId, string? identifier, int? studentId,
                                                     RegistrationMethod method, string userId)
    {
        var session = await _db.AttendanceSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return Fail("الجلسة غير موجودة.");
        if (session.IsClosed) return Fail("الجلسة مغلقة. لا يمكن تسجيل حضور جديد.");

        Student? student;
        if (studentId is int sid)
        {
            method = RegistrationMethod.Manual;
            student = await _db.Students.Include(s => s.Stage).FirstOrDefaultAsync(s => s.Id == sid);
        }
        else
        {
            var code = identifier?.Trim();
            if (string.IsNullOrEmpty(code)) return Fail("لم يتم إدخال أي رمز.");
            if (code.Length > 64) return Fail("الرمز غير صالح.");

            student = method == RegistrationMethod.StudentCode
                ? await _db.Students.Include(s => s.Stage).FirstOrDefaultAsync(s => s.StudentCode == code)
                : await _db.Students.Include(s => s.Stage)
                    .FirstOrDefaultAsync(s => s.QRCodeValue == code || s.StudentCode == code);
        }

        if (student is null) return Fail("لم يتم العثور على طالب بهذا الرمز.");
        if (!student.IsActive) return Fail($"الطالب {student.FullName} موقوف ولا يمكن تسجيل حضوره.");

        if ((session.StageId is not null && student.StageId != session.StageId)
            || (session.GroupId is not null && student.GroupId != session.GroupId))
        {
            return Fail($"الطالب {student.FullName} لا يتبع مرحلة/مجموعة هذه الجلسة.", student);
        }

        var now = _clock.UtcNow;
        var status = await ResolveStatusAsync(session, now);

        var existing = await _db.AttendanceRecords
            .FirstOrDefaultAsync(r => r.AttendanceSessionId == sessionId && r.StudentId == student.Id);

        if (existing is not null)
        {
            if (existing.Status is AttendanceStatus.Present or AttendanceStatus.Late)
            {
                var at = existing.CheckInTime is null ? "" : $" الساعة {_clock.ToLocal(existing.CheckInTime.Value):hh:mm tt}";
                return new RegisterOutcome
                {
                    Succeeded = false,
                    IsDuplicate = true,
                    Message = $"تم تسجيل حضور {student.FullName} مسبقًا{at}.",
                    StudentName = student.FullName,
                    StudentCode = student.StudentCode,
                    StatusText = ArabicText.Status(existing.Status),
                    RegisteredCount = await CountPresentAsync(sessionId)
                };
            }

            // Was marked Absent/Excused earlier (e.g. session was closed then reopened): upgrade it.
            var oldStatus = existing.Status;
            existing.Status = status;
            existing.CheckInTime = now;
            existing.RegistrationMethod = method;
            existing.RegisteredByUserId = userId;
            _audit.Add("AttendanceUpdatedByScan", "AttendanceRecord", existing.Id.ToString(),
                new { Status = oldStatus.ToString() }, new { Status = status.ToString() });
        }
        else
        {
            _db.AttendanceRecords.Add(new AttendanceRecord
            {
                AttendanceSessionId = sessionId,
                StudentId = student.Id,
                Status = status,
                CheckInTime = now,
                RegistrationMethod = method,
                RegisteredByUserId = userId
            });
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index (session, student) tripped by a simultaneous scan.
            _db.ChangeTracker.Clear();
            return new RegisterOutcome
            {
                Succeeded = false,
                IsDuplicate = true,
                Message = $"تم تسجيل حضور {student.FullName} مسبقًا.",
                StudentName = student.FullName,
                StudentCode = student.StudentCode,
                RegisteredCount = await CountPresentAsync(sessionId)
            };
        }

        return new RegisterOutcome
        {
            Succeeded = true,
            Message = status == AttendanceStatus.Late
                ? $"تم تسجيل {student.FullName} (متأخر)."
                : $"تم تسجيل حضور {student.FullName}.",
            StudentName = student.FullName,
            StudentCode = student.StudentCode,
            StatusText = ArabicText.Status(status),
            TimeText = _clock.ToLocal(now).ToString("hh:mm tt"),
            RegisteredCount = await CountPresentAsync(sessionId)
        };
    }

    public async Task<List<StudentSearchResult>> SearchStudentsAsync(int sessionId, string term)
    {
        term = term?.Trim() ?? string.Empty;
        if (term.Length < 1) return new List<StudentSearchResult>();

        var session = await _db.AttendanceSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null) return new List<StudentSearchResult>();

        return await ScopeQuery(session.StageId, session.GroupId)
            .Where(s => s.FullName.Contains(term) || s.StudentCode.Contains(term))
            .OrderBy(s => s.FullName)
            .Take(10)
            .Select(s => new StudentSearchResult
            {
                Id = s.Id,
                Name = s.FullName,
                Code = s.StudentCode,
                Stage = s.Stage.Name,
                Registered = s.AttendanceRecords.Any(r => r.AttendanceSessionId == sessionId
                    && (r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late))
            })
            .ToListAsync();
    }

    // ------------------------------------------------------------------ corrections

    public async Task<ServiceResult> CorrectAsync(int recordId, AttendanceStatus status)
    {
        if (!Enum.IsDefined(status)) return ServiceResult.Fail("حالة الحضور غير صالحة.");
        if (status == AttendanceStatus.Excused && !await AllowExcusedAsync())
            return ServiceResult.Fail("الغياب بعذر غير مفعّل في إعدادات الحضور.");

        var record = await _db.AttendanceRecords.FirstOrDefaultAsync(r => r.Id == recordId);
        if (record is null) return ServiceResult.Fail("سجل الحضور غير موجود.");
        if (record.Status == status) return ServiceResult.Ok();

        var old = new { Status = record.Status.ToString(), record.CheckInTime };

        record.Status = status;
        if (status is AttendanceStatus.Present or AttendanceStatus.Late)
        {
            record.CheckInTime ??= _clock.UtcNow;
        }
        else
        {
            record.CheckInTime = null;
            record.CheckOutTime = null;
        }

        _audit.Add("AttendanceCorrected", "AttendanceRecord", recordId.ToString(), old,
            new { Status = status.ToString(), record.CheckInTime });

        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<bool> AllowExcusedAsync() =>
        !string.Equals(await GetSettingAsync(SettingKeys.AllowExcusedAbsence), "false", StringComparison.OrdinalIgnoreCase);

    // ------------------------------------------------------------------ helpers

    private IQueryable<Student> ScopeQuery(int? stageId, int? groupId) =>
        _db.Students.AsNoTracking().Where(s => s.IsActive
            && (stageId == null || s.StageId == stageId)
            && (groupId == null || s.GroupId == groupId));

    private Task<int> CountPresentAsync(int sessionId) =>
        _db.AttendanceRecords.CountAsync(r => r.AttendanceSessionId == sessionId
            && (r.Status == AttendanceStatus.Present || r.Status == AttendanceStatus.Late));

    private async Task<AttendanceStatus> ResolveStatusAsync(AttendanceSession session, DateTime utcNow)
    {
        var local = _clock.ToLocal(utcNow);

        // "Late" only applies to a session that is happening today.
        if (session.SessionDate != DateOnly.FromDateTime(local)) return AttendanceStatus.Present;

        var setting = await GetSettingAsync(SettingKeys.LateAfterMinutes);
        var lateAfter = int.TryParse(setting, out var minutes) ? minutes : DefaultLateAfterMinutes;
        if (lateAfter <= 0) return AttendanceStatus.Present;

        var cutoff = session.StartTime.ToTimeSpan() + TimeSpan.FromMinutes(lateAfter);
        return TimeOnly.FromDateTime(local).ToTimeSpan() > cutoff ? AttendanceStatus.Late : AttendanceStatus.Present;
    }

    private Task<string?> GetSettingAsync(string key) =>
        _db.ChurchSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();

    private async Task<string?> ValidateSessionAsync(SessionFormViewModel m)
    {
        if (m.SessionDate is null) return "يرجى اختيار تاريخ الجلسة.";
        if (m.StartTime is null) return "يرجى اختيار وقت البدء.";
        if (!Enum.IsDefined(m.SessionType)) return "نوع الجلسة غير صالح.";

        if (m.GroupId is not null && m.StageId is null) return "اختر المرحلة عند اختيار مجموعة.";

        if (m.StageId is not null)
        {
            var stage = await _db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == m.StageId);
            if (stage is null) return "المرحلة المختارة غير موجودة.";
            if (!stage.IsActive) return "المرحلة المختارة غير نشطة.";
        }

        if (m.GroupId is not null)
        {
            var group = await _db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == m.GroupId);
            if (group is null || group.StageId != m.StageId) return "المجموعة المختارة لا تتبع هذه المرحلة.";
            if (!group.IsActive) return "المجموعة المختارة غير نشطة.";
        }

        return null;
    }

    private static RegisterOutcome Fail(string message, Student? student = null) => new()
    {
        Succeeded = false,
        Message = message,
        StudentName = student?.FullName,
        StudentCode = student?.StudentCode
    };
    // Extracted method within AttendanceService for scoring validation
    private async Task<(int Points, bool IsLate)> CalculateAttendancePointsAsync(AttendanceSession session, DateTime checkInTime)
    {
        var settings = await _db.ChurchSettings.ToDictionaryAsync(s => s.Key, s => s.Value);

        int defaultPoints = int.TryParse(settings.GetValueOrDefault("Attendance_DefaultPoints"), out var dp) ? dp : 10;
        int gracePeriod = int.TryParse(settings.GetValueOrDefault("Attendance_GracePeriodMinutes"), out var gp) ? gp : 15;
        string penaltyType = settings.GetValueOrDefault("Attendance_LatePenaltyType") ?? "Percentage"; // "Percentage" or "Fixed"
        double penaltyVal = double.TryParse(settings.GetValueOrDefault("Attendance_LatePenaltyValue"), out var pv) ? pv : 50;

        // تحويل تاريخ ووقت بدء الجلسة إلى DateTime لمقارنتها مع checkInTime
        var sessionStartDateTime = session.SessionDate.ToDateTime(session.StartTime);
        var elapsedTime = (checkInTime - sessionStartDateTime).TotalMinutes;

        if (elapsedTime <= gracePeriod)
        {
            return (defaultPoints, false);
        }

        int calculatedPoints;
        if (penaltyType == "Percentage")
        {
            calculatedPoints = (int)Math.Round(defaultPoints * (penaltyVal / 100.0));
        }
        else
        {
            calculatedPoints = (int)penaltyVal;
        }

        return (calculatedPoints, true);
    }
}
