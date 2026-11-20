using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class PointsService : IPointsService
{
    private const int PageSize = 20;

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAppClock _clock;
    private readonly ISettingsService _settings;

    public PointsService(ApplicationDbContext db, IAuditService audit, IAppClock clock, ISettingsService settings)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
        _settings = settings;
    }

    // ------------------------------------------------------------------ ledger

    public async Task<PointIndexViewModel> SearchAsync(PointListQuery q)
    {
        IQueryable<PointTransaction> query = _db.PointTransactions.AsNoTracking();

        if (q.StudentId is int studentId) query = query.Where(t => t.StudentId == studentId);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(t => t.Student.FullName.Contains(term) || t.Student.StudentCode.Contains(term));
        }

        if (q.Type is PointTransactionType type) query = query.Where(t => t.TransactionType == type);

        if (q.From is DateOnly fromDate)
        {
            var start = _clock.StartOfDayUtc(fromDate);
            query = query.Where(t => t.CreatedAt >= start);
        }

        if (q.To is DateOnly toDate)
        {
            var endExclusive = _clock.StartOfDayUtc(toDate.AddDays(1));
            query = query.Where(t => t.CreatedAt < endExclusive);
        }

        var total = await query.CountAsync();
        var added = await query.Where(t => t.Points > 0).SumAsync(t => (int?)t.Points) ?? 0;
        var deducted = await query.Where(t => t.Points < 0).SumAsync(t => (int?)t.Points) ?? 0;

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(q.Page, 1, totalPages);

        var raw = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(t => new
            {
                t.Id,
                t.CreatedAt,
                t.StudentId,
                Name = t.Student.FullName,
                Code = t.Student.StudentCode,
                t.TransactionType,
                t.Points,
                t.Reason,
                By = t.CreatedBy.FullName != "" ? t.CreatedBy.FullName : t.CreatedBy.UserName,
                t.Notes
            })
            .ToListAsync();

        return new PointIndexViewModel
        {
            Query = q,
            TotalAdded = added,
            TotalDeducted = -deducted,
            Result = new PagedResult<PointListItem>
            {
                Page = page,
                PageSize = PageSize,
                TotalCount = total,
                Items = raw.Select(t => new PointListItem
                {
                    Id = t.Id,
                    LocalDate = _clock.ToLocal(t.CreatedAt),
                    StudentId = t.StudentId,
                    StudentName = t.Name,
                    StudentCode = t.Code,
                    Type = t.TransactionType,
                    Points = t.Points,
                    Reason = t.Reason,
                    CreatedBy = t.By ?? string.Empty,
                    Notes = t.Notes
                }).ToList()
            }
        };
    }

    public Task<List<ReasonOption>> GetReasonOptionsAsync(bool deduction) =>
        _db.PointReasons.AsNoTracking()
            .Where(r => r.IsActive && r.IsDeduction == deduction)
            .OrderBy(r => r.Name)
            .Select(r => new ReasonOption(r.Id, r.Name, r.DefaultPoints))
            .ToListAsync();

    // ------------------------------------------------------------------ award / deduct

    public Task<ServiceResult<int>> AwardAsync(PointFormViewModel m, string userId, int? enforceStageId = null) =>
        ApplyAsync(m, false, userId, enforceStageId);

    public Task<ServiceResult<int>> DeductAsync(PointFormViewModel m, string userId, int? enforceStageId = null) =>
        ApplyAsync(m, true, userId, enforceStageId);

    private async Task<ServiceResult<int>> ApplyAsync(PointFormViewModel m, bool deduction, string userId, int? enforceStageId)
    {
        if (m.StudentId is not int studentId) return ServiceResult<int>.Fail("يرجى اختيار الطالب.");
        if (m.Points is not int points || points < 1 || points > 10000)
            return ServiceResult<int>.Fail("عدد النقاط يجب أن يكون بين 1 و 10000.");
        if (deduction && !m.ConfirmDeduction)
            return ServiceResult<int>.Fail("يرجى تأكيد عملية الخصم.");

        var reason = await _db.PointReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == m.PointReasonId);
        if (reason is null || !reason.IsActive) return ServiceResult<int>.Fail("السبب المختار غير موجود أو غير مفعّل.");
        if (reason.IsDeduction != deduction) return ServiceResult<int>.Fail("هذا السبب لا يناسب نوع العملية.");

        var allowNegative = await _settings.GetBoolAsync(SettingKeys.AllowNegativePoints, false);

        await using var tx = await _db.Database.BeginTransactionAsync();

        // Serialise all balance-changing work for this student (see DbContextExtensions).
        await _db.LockStudentAsync(studentId);

        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        if (student is null) return ServiceResult<int>.Fail("الطالب غير موجود.");
        if (!student.IsActive) return ServiceResult<int>.Fail("الطالب موقوف ولا يمكن تعديل نقاطه.");
        if (enforceStageId is not null && student.StageId != enforceStageId)
            return ServiceResult<int>.Fail("الطالب لا يتبع المرحلة الحالية.");

        var signed = deduction ? -points : points;

        if (deduction && !allowNegative)
        {
            var balance = await _db.PointTransactions.Where(t => t.StudentId == studentId)
                .SumAsync(t => (int?)t.Points) ?? 0;

            if (balance + signed < 0)
                return ServiceResult<int>.Fail($"رصيد الطالب ({balance:N0}) لا يكفي لخصم {points:N0} نقطة.");
        }

        var transaction = new PointTransaction
        {
            StudentId = studentId,
            TransactionType = deduction ? PointTransactionType.Deduct : PointTransactionType.Earn,
            Points = signed,
            Reason = reason.Name,
            PointReasonId = reason.Id,
            CreatedByUserId = userId,
            CreatedAt = _clock.UtcNow,
            Notes = string.IsNullOrWhiteSpace(m.Notes) ? null : m.Notes.Trim()
        };

        _db.PointTransactions.Add(transaction);
        await _db.SaveChangesAsync();

        _audit.Add(deduction ? "PointsDeducted" : "PointsAwarded", "PointTransaction", transaction.Id.ToString(),
            null, new { studentId, Points = signed, Reason = reason.Name });
        await _db.SaveChangesAsync();

        await tx.CommitAsync();
        return ServiceResult<int>.Ok(transaction.Id);
    }

    // ------------------------------------------------------------------ reasons (admin)

    public Task<List<PointReasonListItem>> ListReasonsAsync() =>
        _db.PointReasons.AsNoTracking()
            .OrderBy(r => r.IsDeduction).ThenBy(r => r.Name)
            .Select(r => new PointReasonListItem
            {
                Id = r.Id,
                Name = r.Name,
                DefaultPoints = r.DefaultPoints,
                IsDeduction = r.IsDeduction,
                IsActive = r.IsActive
            })
            .ToListAsync();

    public Task<PointReason?> GetReasonAsync(int id) =>
        _db.PointReasons.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<ServiceResult> SaveReasonAsync(PointReasonFormViewModel m)
    {
        var name = m.Name.Trim();
        var id = m.Id ?? 0;

        if (await _db.PointReasons.AnyAsync(r => r.Name == name && r.IsDeduction == m.IsDeduction && r.Id != id))
            return ServiceResult.Fail("يوجد سبب بنفس الاسم والنوع.");

        if (m.Id is null)
        {
            var reason = new PointReason { Name = name, DefaultPoints = m.DefaultPoints, IsDeduction = m.IsDeduction };
            _db.PointReasons.Add(reason);
            await _db.SaveChangesAsync();
            _audit.Add("PointReasonCreated", "PointReason", reason.Id.ToString(), null, new { reason.Name, reason.DefaultPoints, reason.IsDeduction });
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        var existing = await _db.PointReasons.FirstOrDefaultAsync(r => r.Id == id);
        if (existing is null) return ServiceResult.Fail("السبب غير موجود.");

        var old = new { existing.Name, existing.DefaultPoints, existing.IsDeduction };
        existing.Name = name;
        existing.DefaultPoints = m.DefaultPoints;
        existing.IsDeduction = m.IsDeduction;
        _audit.Add("PointReasonUpdated", "PointReason", id.ToString(), old, new { existing.Name, existing.DefaultPoints, existing.IsDeduction });
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetReasonActiveAsync(int id, bool active)
    {
        var reason = await _db.PointReasons.FirstOrDefaultAsync(r => r.Id == id);
        if (reason is null) return ServiceResult.Fail("السبب غير موجود.");

        reason.IsActive = active;
        _audit.Add(active ? "PointReasonActivated" : "PointReasonDeactivated", "PointReason", id.ToString());
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
