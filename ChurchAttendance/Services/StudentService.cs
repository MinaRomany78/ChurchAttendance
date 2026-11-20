using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class StudentService : IStudentService
{
    private const int PageSize = 15;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IQrCodeService _qr;
    private readonly IAppClock _clock;
    private readonly IChurchInfoService _church;

    public StudentService(ApplicationDbContext db, IAuditService audit, IQrCodeService qr,
                          IAppClock clock, IChurchInfoService church)
    {
        _db = db;
        _audit = audit;
        _qr = qr;
        _clock = clock;
        _church = church;
    }

    // ------------------------------------------------------------------ list

    public async Task<PagedResult<StudentListItem>> SearchAsync(StudentListQuery q)
    {
        IQueryable<Student> query = _db.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(s => s.FullName.Contains(term)
                                     || s.StudentCode.Contains(term)
                                     || s.QRCodeValue == term);
        }

        if (q.StageId is int stageId) query = query.Where(s => s.StageId == stageId);
        if (q.GroupId is int groupId) query = query.Where(s => s.GroupId == groupId);

        query = q.Status switch
        {
            "active" => query.Where(s => s.IsActive),
            "inactive" => query.Where(s => !s.IsActive),
            _ => query
        };

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(q.Page, 1, totalPages);

        var items = await ApplySort(query, q.Sort, q.Desc)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(s => new StudentListItem
            {
                Id = s.Id,
                StudentCode = s.StudentCode,
                FullName = s.FullName,
                StageName = s.Stage.Name,
                GroupName = s.Group != null ? s.Group.Name : null,
                Points = s.PointTransactions.Sum(p => (int?)p.Points) ?? 0,
                IsActive = s.IsActive
            })
            .ToListAsync();

        return new PagedResult<StudentListItem>
        {
            Items = items,
            Page = page,
            PageSize = PageSize,
            TotalCount = total
        };
    }

    private static IQueryable<Student> ApplySort(IQueryable<Student> q, string? sort, bool desc) => sort switch
    {
        "code" => desc ? q.OrderByDescending(s => s.StudentCode).ThenBy(s => s.Id)
                       : q.OrderBy(s => s.StudentCode).ThenBy(s => s.Id),
        "stage" => desc ? q.OrderByDescending(s => s.Stage.SortOrder).ThenBy(s => s.FullName)
                        : q.OrderBy(s => s.Stage.SortOrder).ThenBy(s => s.FullName),
        "points" => desc ? q.OrderByDescending(s => s.PointTransactions.Sum(p => (int?)p.Points) ?? 0).ThenBy(s => s.Id)
                         : q.OrderBy(s => s.PointTransactions.Sum(p => (int?)p.Points) ?? 0).ThenBy(s => s.Id),
        "status" => desc ? q.OrderByDescending(s => s.IsActive).ThenBy(s => s.FullName)
                         : q.OrderBy(s => s.IsActive).ThenBy(s => s.FullName),
        _ => desc ? q.OrderByDescending(s => s.FullName).ThenBy(s => s.Id)
                  : q.OrderBy(s => s.FullName).ThenBy(s => s.Id)
    };

    // ------------------------------------------------------------------ read

    public async Task<StudentFormViewModel?> GetForEditAsync(int id)
    {
        var s = await _db.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;

        return new StudentFormViewModel
        {
            Id = s.Id,
            CurrentStageId = s.StageId,
            StudentCode = s.StudentCode,
            FullName = s.FullName,
            DateOfBirth = s.DateOfBirth,
            Gender = s.Gender,
            Phone = s.Phone,
            GuardianName = s.GuardianName,
            GuardianPhone = s.GuardianPhone,
            Address = s.Address,
            StageId = s.StageId,
            GroupId = s.GroupId
        };
    }

    public async Task<StudentDetailsViewModel?> GetDetailsAsync(int id, bool includeQr)
    {
        var s = await _db.Students.AsNoTracking()
            .Include(x => x.Stage).Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;

        var counts = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == id)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int Count(AttendanceStatus st) => counts.FirstOrDefault(c => c.Status == st)?.Count ?? 0;
        var present = Count(AttendanceStatus.Present);
        var late = Count(AttendanceStatus.Late);
        var absent = Count(AttendanceStatus.Absent);
        var excused = Count(AttendanceStatus.Excused);
        var attended = present + late;

        var attendanceRaw = await _db.AttendanceRecords.AsNoTracking()
            .Where(r => r.StudentId == id)
            .OrderByDescending(r => r.AttendanceSession.SessionDate).ThenByDescending(r => r.Id)
            .Take(10)
            .Select(r => new
            {
                r.AttendanceSession.SessionDate,
                r.AttendanceSession.Title,
                r.Status,
                r.RegistrationMethod,
                r.CheckInTime
            })
            .ToListAsync();

        var transactionsRaw = await _db.PointTransactions.AsNoTracking()
            .Where(t => t.StudentId == id)
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Take(15)
            .Select(t => new
            {
                t.CreatedAt,
                t.TransactionType,
                t.Points,
                t.Reason,
                By = t.CreatedBy.FullName != "" ? t.CreatedBy.FullName : t.CreatedBy.UserName
            })
            .ToListAsync();

        var redemptionsRaw = await _db.RewardRedemptions.AsNoTracking()
            .Where(r => r.StudentId == id)
            .OrderByDescending(r => r.RedeemedAt)
            .Take(10)
            .Select(r => new { r.RedeemedAt, RewardName = r.Reward.Name, r.PointsUsed, r.RedemptionStatus })
            .ToListAsync();

        return new StudentDetailsViewModel
        {
            Id = s.Id,
            StageId = s.StageId,
            StudentCode = s.StudentCode,
            FullName = s.FullName,
            DateOfBirth = s.DateOfBirth,
            Gender = s.Gender,
            Phone = s.Phone,
            GuardianName = s.GuardianName,
            GuardianPhone = s.GuardianPhone,
            Address = s.Address,
            StageName = s.Stage.Name,
            GroupName = s.Group?.Name,
            IsActive = s.IsActive,
            CreatedAtLocal = _clock.ToLocal(s.CreatedAt),
            Balance = await _db.PointTransactions.Where(t => t.StudentId == id).SumAsync(t => (int?)t.Points) ?? 0,
            PresentCount = present,
            LateCount = late,
            AbsentCount = absent,
            ExcusedCount = excused,
            AttendanceRate = attended + absent > 0 ? Math.Round(100.0 * attended / (attended + absent), 1) : 0,
            QrDataUri = includeQr ? _qr.ToPngDataUri(s.QRCodeValue, 8) : null,
            CanManage = includeQr,
            Attendance = attendanceRaw.Select(a => new AttendanceHistoryItem
            {
                Date = a.SessionDate,
                SessionTitle = a.Title,
                Status = a.Status,
                Method = a.RegistrationMethod,
                LocalCheckIn = a.CheckInTime is null ? null : _clock.ToLocal(a.CheckInTime.Value)
            }).ToList(),
            Transactions = transactionsRaw.Select(t => new PointHistoryItem
            {
                LocalDate = _clock.ToLocal(t.CreatedAt),
                Type = t.TransactionType,
                Points = t.Points,
                Reason = t.Reason,
                CreatedBy = t.By ?? string.Empty
            }).ToList(),
            Redemptions = redemptionsRaw.Select(r => new RedemptionHistoryItem
            {
                LocalDate = _clock.ToLocal(r.RedeemedAt),
                RewardName = r.RewardName,
                PointsUsed = r.PointsUsed,
                Status = r.RedemptionStatus
            }).ToList()
        };
    }

    public async Task<BadgeViewModel?> GetBadgeAsync(int id)
    {
        var s = await _db.Students.AsNoTracking()
            .Include(x => x.Stage).Include(x => x.Group)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return null;

        var church = await _church.GetAsync();
        return new BadgeViewModel
        {
            ChurchName = church.Name,
            LogoPath = church.LogoPath,
            StudentName = s.FullName,
            StudentCode = s.StudentCode,
            StageName = s.Stage.Name,
            GroupName = s.Group?.Name,
            QrDataUri = _qr.ToPngDataUri(s.QRCodeValue, 10)
        };
    }

    // ------------------------------------------------------------------ write

    public async Task<ServiceResult<int>> CreateAsync(StudentFormViewModel m)
    {
        var error = await ValidateAsync(m, null, null);
        if (error is not null) return ServiceResult<int>.Fail(error);

        var code = m.StudentCode?.Trim();
        var autoCode = string.IsNullOrEmpty(code);

        if (!autoCode && await _db.Students.AnyAsync(s => s.StudentCode == code))
            return ServiceResult<int>.Fail("كود الطالب مستخدم بالفعل.");

        var student = new Student
        {
            // Auto codes get a temporary unique value first, then ST + Id once the row exists.
            StudentCode = autoCode ? "TMP-" + Guid.NewGuid().ToString("N")[..20] : code!,
            FullName = m.FullName.Trim(),
            DateOfBirth = m.DateOfBirth,
            Gender = m.Gender,
            Phone = Clean(m.Phone),
            GuardianName = Clean(m.GuardianName),
            GuardianPhone = Clean(m.GuardianPhone),
            Address = Clean(m.Address),
            StageId = m.StageId!.Value,
            GroupId = m.GroupId,
            QRCodeValue = _qr.GenerateToken(),
            IsActive = true,
            CreatedAt = _clock.UtcNow
        };

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            _db.Students.Add(student);
            await _db.SaveChangesAsync();

            if (autoCode)
            {
                student.StudentCode = $"ST{student.Id:D5}";
                await _db.SaveChangesAsync();
            }

            _audit.Add("StudentCreated", "Student", student.Id.ToString(), null, Snapshot(student));
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult<int>.Ok(student.Id);
        }
        catch (DbUpdateException)
        {
            return ServiceResult<int>.Fail("تعذر حفظ الطالب لأن الكود مستخدم بالفعل. جرّب كودًا آخر.");
        }
    }

    public async Task<ServiceResult> UpdateAsync(StudentFormViewModel m)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == m.Id);
        if (student is null) return ServiceResult.Fail("الطالب غير موجود.");

        var error = await ValidateAsync(m, student.StageId, student.GroupId);
        if (error is not null) return ServiceResult.Fail(error);

        var code = string.IsNullOrWhiteSpace(m.StudentCode) ? student.StudentCode : m.StudentCode.Trim();
        if (await _db.Students.AnyAsync(s => s.StudentCode == code && s.Id != student.Id))
            return ServiceResult.Fail("كود الطالب مستخدم بالفعل.");

        var old = Snapshot(student);

        student.StudentCode = code;
        student.FullName = m.FullName.Trim();
        student.DateOfBirth = m.DateOfBirth;
        student.Gender = m.Gender;
        student.Phone = Clean(m.Phone);
        student.GuardianName = Clean(m.GuardianName);
        student.GuardianPhone = Clean(m.GuardianPhone);
        student.Address = Clean(m.Address);
        student.StageId = m.StageId!.Value;
        student.GroupId = m.GroupId;
        student.UpdatedAt = _clock.UtcNow;

        _audit.Add("StudentUpdated", "Student", student.Id.ToString(), old, Snapshot(student));

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Fail("تعذر حفظ التعديلات. تأكد من عدم تكرار كود الطالب.");
        }

        return ServiceResult.Ok();
    }

    /// <summary>Soft deactivation only: attendance, points and redemptions stay intact.</summary>
    public async Task<ServiceResult> SetActiveAsync(int id, bool active)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return ServiceResult.Fail("الطالب غير موجود.");
        if (student.IsActive == active) return ServiceResult.Ok();

        student.IsActive = active;
        student.UpdatedAt = _clock.UtcNow;
        _audit.Add(active ? "StudentActivated" : "StudentDeactivated", "Student", id.ToString());
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> RegenerateQrAsync(int id)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == id);
        if (student is null) return ServiceResult.Fail("الطالب غير موجود.");

        student.QRCodeValue = _qr.GenerateToken();
        student.UpdatedAt = _clock.UtcNow;
        // The token itself is deliberately not written to the audit log.
        _audit.Add("StudentQrRegenerated", "Student", id.ToString());
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // ------------------------------------------------------------------ lookup (student picker)

    public async Task<List<StudentLookupItem>> SearchLookupAsync(string term, int? stageId = null)
    {
        term = term?.Trim() ?? string.Empty;
        if (term.Length == 0) return new List<StudentLookupItem>();

        return await _db.Students.AsNoTracking()
            .Where(s => s.IsActive && (s.FullName.Contains(term) || s.StudentCode.Contains(term)))
            .Where(s => stageId == null || s.StageId == stageId)
            .OrderBy(s => s.FullName)
            .Take(10)
            .Select(s => new StudentLookupItem
            {
                Id = s.Id,
                StageId = s.StageId,
                Name = s.FullName,
                Code = s.StudentCode,
                Stage = s.Stage.Name,
                Balance = s.PointTransactions.Sum(p => (int?)p.Points) ?? 0
            })
            .ToListAsync();
    }

    public Task<StudentLookupItem?> GetLookupAsync(int id) =>
        _db.Students.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new StudentLookupItem
            {
                Id = s.Id,
                StageId = s.StageId,
                Name = s.FullName,
                Code = s.StudentCode,
                Stage = s.Stage.Name,
                Balance = s.PointTransactions.Sum(p => (int?)p.Points) ?? 0
            })
            .FirstOrDefaultAsync();

    // ------------------------------------------------------------------ helpers

    private async Task<string?> ValidateAsync(StudentFormViewModel m, int? currentStageId, int? currentGroupId)
    {
        if (m.StageId is null) return "يرجى اختيار المرحلة.";

        var stage = await _db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == m.StageId);
        if (stage is null) return "المرحلة المختارة غير موجودة.";
        if (!stage.IsActive && stage.Id != currentStageId) return "المرحلة المختارة غير نشطة.";

        if (m.GroupId is not null)
        {
            var group = await _db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == m.GroupId);
            if (group is null || group.StageId != m.StageId) return "المجموعة المختارة لا تتبع هذه المرحلة.";
            if (!group.IsActive && group.Id != currentGroupId) return "المجموعة المختارة غير نشطة.";
        }

        if (m.DateOfBirth is DateOnly dob && dob > _clock.Today)
            return "تاريخ الميلاد لا يمكن أن يكون في المستقبل.";

        return null;
    }

    // Minimal on purpose: the audit log must not become a copy of personal data.
    private static object Snapshot(Student s) => new { s.StudentCode, s.FullName, s.StageId, s.GroupId, s.IsActive };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
