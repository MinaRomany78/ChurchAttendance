using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class RewardService : IRewardService
{
    private const int PageSize = 20;
    private const string RedemptionRef = "RewardRedemption";

    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAppClock _clock;

    public RewardService(ApplicationDbContext db, IAuditService audit, IAppClock clock)
    {
        _db = db;
        _audit = audit;
        _clock = clock;
    }

    // ------------------------------------------------------------------ catalog

    public Task<List<RewardListItem>> ListAsync(bool includeInactive) =>
        _db.Rewards.AsNoTracking()
            .Where(r => includeInactive || r.IsActive)
            .OrderByDescending(r => r.IsActive).ThenBy(r => r.RequiredPoints).ThenBy(r => r.Name)
            .Select(r => new RewardListItem
            {
                Id = r.Id,
                Name = r.Name,
                Description = r.Description,
                RequiredPoints = r.RequiredPoints,
                StockQuantity = r.StockQuantity,
                IsActive = r.IsActive,
                RedemptionCount = _db.RewardRedemptions.Count(x => x.RewardId == r.Id
                                                                   && x.RedemptionStatus != RedemptionStatus.Cancelled)
            })
            .ToListAsync();

    public Task<List<RewardOption>> GetActiveOptionsAsync() =>
        _db.Rewards.AsNoTracking()
            .Where(r => r.IsActive && (r.StockQuantity == null || r.StockQuantity > 0))
            .OrderBy(r => r.RequiredPoints).ThenBy(r => r.Name)
            .Select(r => new RewardOption(r.Id, r.Name, r.RequiredPoints, r.StockQuantity))
            .ToListAsync();

    public Task<Reward?> GetAsync(int id) =>
        _db.Rewards.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<ServiceResult> SaveAsync(RewardFormViewModel m)
    {
        if (m.RequiredPoints is not int requiredPoints) return ServiceResult.Fail("يرجى إدخال النقاط المطلوبة.");

        var name = m.Name.Trim();
        var id = m.Id ?? 0;
        var description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim();

        if (await _db.Rewards.AnyAsync(r => r.Name == name && r.Id != id))
            return ServiceResult.Fail("يوجد مكافأة بنفس الاسم.");

        if (m.Id is null)
        {
            var reward = new Reward
            {
                Name = name,
                Description = description,
                RequiredPoints = requiredPoints,
                StockQuantity = m.StockQuantity,
                CreatedAt = _clock.UtcNow
            };
            _db.Rewards.Add(reward);
            await _db.SaveChangesAsync();
            _audit.Add("RewardCreated", "Reward", reward.Id.ToString(), null, new { reward.Name, reward.RequiredPoints, reward.StockQuantity });
            await _db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        var existing = await _db.Rewards.FirstOrDefaultAsync(r => r.Id == id);
        if (existing is null) return ServiceResult.Fail("المكافأة غير موجودة.");

        var old = new { existing.Name, existing.RequiredPoints, existing.StockQuantity };
        existing.Name = name;
        existing.Description = description;
        existing.RequiredPoints = requiredPoints;
        existing.StockQuantity = m.StockQuantity;
        _audit.Add("RewardUpdated", "Reward", id.ToString(), old, new { existing.Name, existing.RequiredPoints, existing.StockQuantity });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("تم تعديل هذه المكافأة من مستخدم آخر أثناء التعديل. أعد فتح الصفحة وحاول مرة أخرى.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetActiveAsync(int id, bool active)
    {
        var reward = await _db.Rewards.FirstOrDefaultAsync(r => r.Id == id);
        if (reward is null) return ServiceResult.Fail("المكافأة غير موجودة.");

        reward.IsActive = active;
        _audit.Add(active ? "RewardActivated" : "RewardDeactivated", "Reward", id.ToString());
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    // ------------------------------------------------------------------ redemption

    public async Task<ServiceResult<RedeemPreviewViewModel>> PreviewRedeemAsync(int studentId, int rewardId, string? notes, int? enforceStageId = null)
    {
        var student = await _db.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Id == studentId);
        var reward = await _db.Rewards.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rewardId);
        var balance = await BalanceAsync(studentId);

        var error = Check(student, reward, balance, enforceStageId);
        if (error is not null) return ServiceResult<RedeemPreviewViewModel>.Fail(error);

        return ServiceResult<RedeemPreviewViewModel>.Ok(new RedeemPreviewViewModel
        {
            StudentId = student!.Id,
            StudentName = student.FullName,
            StudentCode = student.StudentCode,
            Balance = balance,
            RewardId = reward!.Id,
            RewardName = reward.Name,
            RequiredPoints = reward.RequiredPoints,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
    }

    public async Task<ServiceResult<int>> RedeemAsync(int studentId, int rewardId, string? notes, string userId, int? enforceStageId = null)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1) Serialise this student's balance changes, THEN read the balance.
            await _db.LockStudentAsync(studentId);

            var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId);
            var reward = await _db.Rewards.FirstOrDefaultAsync(r => r.Id == rewardId); // tracked: RowVersion guards stock
            var balance = await BalanceAsync(studentId);

            var error = Check(student, reward, balance, enforceStageId);
            if (error is not null) return ServiceResult<int>.Fail(error);

            // 2) Stock, points and redemption change together or not at all.
            if (reward!.StockQuantity is int stock) reward.StockQuantity = stock - 1;

            var redemption = new RewardRedemption
            {
                StudentId = studentId,
                RewardId = rewardId,
                PointsUsed = reward.RequiredPoints,
                RedemptionStatus = RedemptionStatus.Pending,
                RedeemedAt = _clock.UtcNow,
                RequestedByUserId = userId,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
            };

            _db.RewardRedemptions.Add(redemption);
            await _db.SaveChangesAsync();

            _db.PointTransactions.Add(new PointTransaction
            {
                StudentId = studentId,
                TransactionType = PointTransactionType.Redemption,
                Points = -reward.RequiredPoints,
                Reason = $"استبدال مكافأة: {reward.Name}",
                ReferenceType = RedemptionRef,
                ReferenceId = redemption.Id,
                CreatedByUserId = userId,
                CreatedAt = _clock.UtcNow
            });

            _audit.Add("RewardRedeemed", "RewardRedemption", redemption.Id.ToString(), null,
                new { studentId, rewardId, Points = reward.RequiredPoints });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult<int>.Ok(redemption.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else changed this reward's stock at the same moment.
            return ServiceResult<int>.Fail("تغيّرت كمية المكافأة أثناء العملية. أعد المحاولة.");
        }
    }

    public async Task<ServiceResult> ApproveAsync(int redemptionId, string userId)
    {
        var r = await _db.RewardRedemptions.FirstOrDefaultAsync(x => x.Id == redemptionId);
        if (r is null) return ServiceResult.Fail("سجل الاستبدال غير موجود.");
        if (r.RedemptionStatus != RedemptionStatus.Pending) return ServiceResult.Fail("لا يمكن الموافقة إلا على طلب قيد المراجعة.");

        r.RedemptionStatus = RedemptionStatus.Approved;
        r.ApprovedByUserId = userId;
        r.ApprovedAt = _clock.UtcNow;
        _audit.Add("RedemptionApproved", "RewardRedemption", redemptionId.ToString());
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> DeliverAsync(int redemptionId, string userId)
    {
        var r = await _db.RewardRedemptions.FirstOrDefaultAsync(x => x.Id == redemptionId);
        if (r is null) return ServiceResult.Fail("سجل الاستبدال غير موجود.");
        if (r.RedemptionStatus is not (RedemptionStatus.Pending or RedemptionStatus.Approved))
            return ServiceResult.Fail("لا يمكن تسليم هذا الطلب.");

        r.RedemptionStatus = RedemptionStatus.Delivered;
        r.ApprovedByUserId ??= userId;
        r.ApprovedAt ??= _clock.UtcNow;
        _audit.Add("RedemptionDelivered", "RewardRedemption", redemptionId.ToString());
        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> CancelAsync(int redemptionId, string userId)
    {
        var studentId = await _db.RewardRedemptions.Where(x => x.Id == redemptionId)
            .Select(x => (int?)x.StudentId).FirstOrDefaultAsync();
        if (studentId is null) return ServiceResult.Fail("سجل الاستبدال غير موجود.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            await _db.LockStudentAsync(studentId.Value);

            // Load AFTER the lock so a simultaneous cancel cannot refund twice.
            var r = await _db.RewardRedemptions.Include(x => x.Reward).FirstOrDefaultAsync(x => x.Id == redemptionId);
            if (r is null) return ServiceResult.Fail("سجل الاستبدال غير موجود.");
            if (r.RedemptionStatus is not (RedemptionStatus.Pending or RedemptionStatus.Approved))
                return ServiceResult.Fail("لا يمكن إلغاء استبدال تم تسليمه أو ألغي مسبقًا.");

            r.RedemptionStatus = RedemptionStatus.Cancelled;
            if (r.Reward.StockQuantity is int stock) r.Reward.StockQuantity = stock + 1;

            _db.PointTransactions.Add(new PointTransaction
            {
                StudentId = r.StudentId,
                TransactionType = PointTransactionType.Refund,
                Points = r.PointsUsed,
                Reason = $"استرجاع نقاط: إلغاء استبدال {r.Reward.Name}",
                ReferenceType = RedemptionRef,
                ReferenceId = r.Id,
                CreatedByUserId = userId,
                CreatedAt = _clock.UtcNow
            });

            _audit.Add("RedemptionCancelled", "RewardRedemption", redemptionId.ToString(), null,
                new { r.StudentId, RefundedPoints = r.PointsUsed });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return ServiceResult.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult.Fail("تغيّرت بيانات المكافأة أثناء العملية. أعد المحاولة.");
        }
    }

    public async Task<PagedResult<RedemptionListItem>> SearchRedemptionsAsync(RedemptionListQuery q)
    {
        IQueryable<RewardRedemption> query = _db.RewardRedemptions.AsNoTracking();

        if (q.Status is RedemptionStatus status) query = query.Where(r => r.RedemptionStatus == status);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(r => r.Student.FullName.Contains(term)
                                     || r.Student.StudentCode.Contains(term)
                                     || r.Reward.Name.Contains(term));
        }

        if (q.From is DateOnly fromDate)
        {
            var start = _clock.StartOfDayUtc(fromDate);
            query = query.Where(r => r.RedeemedAt >= start);
        }

        if (q.To is DateOnly toDate)
        {
            var endExclusive = _clock.StartOfDayUtc(toDate.AddDays(1));
            query = query.Where(r => r.RedeemedAt < endExclusive);
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(q.Page, 1, totalPages);

        var raw = await query
            .OrderByDescending(r => r.RedeemedAt).ThenByDescending(r => r.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(r => new
            {
                r.Id,
                r.RedeemedAt,
                r.StudentId,
                Name = r.Student.FullName,
                Code = r.Student.StudentCode,
                RewardName = r.Reward.Name,
                r.PointsUsed,
                r.RedemptionStatus,
                r.Notes
            })
            .ToListAsync();

        return new PagedResult<RedemptionListItem>
        {
            Page = page,
            PageSize = PageSize,
            TotalCount = total,
            Items = raw.Select(r => new RedemptionListItem
            {
                Id = r.Id,
                LocalDate = _clock.ToLocal(r.RedeemedAt),
                StudentId = r.StudentId,
                StudentName = r.Name,
                StudentCode = r.Code,
                RewardName = r.RewardName,
                PointsUsed = r.PointsUsed,
                Status = r.RedemptionStatus,
                Notes = r.Notes
            }).ToList()
        };
    }

    // ------------------------------------------------------------------ helpers

    private async Task<int> BalanceAsync(int studentId) =>
        await _db.PointTransactions.Where(t => t.StudentId == studentId).SumAsync(t => (int?)t.Points) ?? 0;

    /// <summary>Single set of rules used by both the preview and the real redemption.</summary>
    private static string? Check(Student? student, Reward? reward, int balance, int? enforceStageId = null)
    {
        if (student is null) return "الطالب غير موجود.";
        if (!student.IsActive) return "الطالب موقوف ولا يمكنه استبدال مكافآت.";
        if (enforceStageId is not null && student.StageId != enforceStageId) return "الطالب لا يتبع المرحلة الحالية.";
        if (reward is null) return "المكافأة غير موجودة.";
        if (!reward.IsActive) return "هذه المكافأة غير متاحة حاليًا.";
        if (reward.StockQuantity is <= 0) return "نفدت كمية هذه المكافأة.";
        if (balance < reward.RequiredPoints)
            return $"رصيد الطالب ({balance:N0}) لا يكفي. المطلوب {reward.RequiredPoints:N0} نقطة.";
        return null;
    }
}
