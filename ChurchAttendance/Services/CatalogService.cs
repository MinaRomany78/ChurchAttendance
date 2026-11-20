using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class CatalogService : ICatalogService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAccessCodeHasher _hasher;

    public CatalogService(ApplicationDbContext db, IAuditService audit, IAccessCodeHasher hasher)
    {
        _db = db;
        _audit = audit;
        _hasher = hasher;
    }

    // ---------------- Stages ----------------

    public Task<List<StageListItem>> ListStagesAsync() =>
        _db.Stages.AsNoTracking()
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new StageListItem
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                SortOrder = s.SortOrder,
                IsActive = s.IsActive,
                GroupCount = s.Groups.Count,
                StudentCount = s.Students.Count(x => x.IsActive),
                RequiresCode = s.AccessCodeHash != null && s.AccessCodeHash != ""
            })
            .ToListAsync();

    public Task<Stage?> GetStageAsync(int id) =>
        _db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

    public async Task<ServiceResult> SaveStageAsync(StageFormViewModel m)
    {
        var name = m.Name.Trim();
        var id = m.Id ?? 0;

        if (await _db.Stages.AnyAsync(s => s.Name == name && s.Id != id))
            return ServiceResult.Fail("اسم المرحلة مستخدم بالفعل.");

        if (m.Id is null)
        {
            var stage = new Stage { Name = name, Description = Clean(m.Description), SortOrder = m.SortOrder };
            _db.Stages.Add(stage);
            await SaveAsync();
            _audit.Add("StageCreated", "Stage", stage.Id.ToString(), null, new { stage.Name, stage.SortOrder });
            await SaveAsync();
            return ServiceResult.Ok();
        }

        var existing = await _db.Stages.FirstOrDefaultAsync(s => s.Id == id);
        if (existing is null) return ServiceResult.Fail("المرحلة غير موجودة.");

        var old = new { existing.Name, existing.Description, existing.SortOrder };
        existing.Name = name;
        existing.Description = Clean(m.Description);
        existing.SortOrder = m.SortOrder;
        _audit.Add("StageUpdated", "Stage", existing.Id.ToString(), old, new { existing.Name, existing.Description, existing.SortOrder });
        await SaveAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetStageActiveAsync(int id, bool active)
    {
        var stage = await _db.Stages.FirstOrDefaultAsync(s => s.Id == id);
        if (stage is null) return ServiceResult.Fail("المرحلة غير موجودة.");
        if (stage.IsActive == active) return ServiceResult.Ok();

        if (!active && await _db.Students.AnyAsync(s => s.StageId == id && s.IsActive))
            return ServiceResult.Fail("لا يمكن إيقاف مرحلة بها طلاب نشطون. انقل الطلاب إلى مرحلة أخرى أو أوقفهم أولًا.");

        stage.IsActive = active;
        _audit.Add(active ? "StageActivated" : "StageDeactivated", "Stage", id.ToString());
        await SaveAsync();
        return ServiceResult.Ok();
    }

    // ---------------- Groups ----------------

    public Task<List<GroupListItem>> ListGroupsAsync(int? stageId) =>
        _db.Groups.AsNoTracking()
            .Where(g => stageId == null || g.StageId == stageId)
            .OrderBy(g => g.Stage.SortOrder).ThenBy(g => g.Name)
            .Select(g => new GroupListItem
            {
                Id = g.Id,
                Name = g.Name,
                StageName = g.Stage.Name,
                Description = g.Description,
                IsActive = g.IsActive,
                StudentCount = g.Students.Count(x => x.IsActive)
            })
            .ToListAsync();

    public Task<StudentGroup?> GetGroupAsync(int id) =>
        _db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);

    public async Task<ServiceResult> SaveGroupAsync(GroupFormViewModel m)
    {
        var name = m.Name.Trim();
        var id = m.Id ?? 0;

        if (m.StageId is null || !await _db.Stages.AnyAsync(s => s.Id == m.StageId))
            return ServiceResult.Fail("المرحلة المختارة غير موجودة.");

        var stageId = m.StageId.Value;

        if (await _db.Groups.AnyAsync(g => g.StageId == stageId && g.Name == name && g.Id != id))
            return ServiceResult.Fail("يوجد مجموعة بنفس الاسم داخل هذه المرحلة.");

        if (m.Id is null)
        {
            var group = new StudentGroup { Name = name, StageId = stageId, Description = Clean(m.Description) };
            _db.Groups.Add(group);
            await SaveAsync();
            _audit.Add("GroupCreated", "Group", group.Id.ToString(), null, new { group.Name, group.StageId });
            await SaveAsync();
            return ServiceResult.Ok();
        }

        var existing = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id);
        if (existing is null) return ServiceResult.Fail("المجموعة غير موجودة.");

        if (existing.StageId != stageId && await _db.Students.AnyAsync(s => s.GroupId == id))
            return ServiceResult.Fail("لا يمكن نقل مجموعة بها طلاب إلى مرحلة أخرى.");

        var old = new { existing.Name, existing.StageId, existing.Description };
        existing.Name = name;
        existing.StageId = stageId;
        existing.Description = Clean(m.Description);
        _audit.Add("GroupUpdated", "Group", existing.Id.ToString(), old, new { existing.Name, existing.StageId, existing.Description });
        await SaveAsync();
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetGroupActiveAsync(int id, bool active)
    {
        var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return ServiceResult.Fail("المجموعة غير موجودة.");
        if (group.IsActive == active) return ServiceResult.Ok();

        if (!active && await _db.Students.AnyAsync(s => s.GroupId == id && s.IsActive))
            return ServiceResult.Fail("لا يمكن إيقاف مجموعة بها طلاب نشطون. انقل الطلاب إلى مجموعة أخرى أو أوقفهم أولًا.");

        group.IsActive = active;
        _audit.Add(active ? "GroupActivated" : "GroupDeactivated", "Group", id.ToString());
        await SaveAsync();
        return ServiceResult.Ok();
    }

    // ---------------- Options for dropdowns ----------------

    public Task<List<StageOption>> GetStageOptionsAsync(int? includeId = null, bool includeInactive = false) =>
        _db.Stages.AsNoTracking()
            .Where(s => includeInactive || s.IsActive || s.Id == includeId)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new StageOption(s.Id, s.Name))
            .ToListAsync();

    public Task<List<GroupOption>> GetGroupOptionsAsync(int? includeId = null, bool includeInactive = false, int? stageId = null) =>
        _db.Groups.AsNoTracking()
            .Where(g => includeInactive || g.IsActive || g.Id == includeId)
            .Where(g => stageId == null || g.StageId == stageId)
            .OrderBy(g => g.Stage.SortOrder).ThenBy(g => g.Name)
            .Select(g => new GroupOption(g.Id, g.Name, g.StageId))
            .ToListAsync();

    // ---------------- Stage selection & access codes ----------------

    public Task<List<StageSelectionItem>> GetSelectionListAsync() =>
        _db.Stages.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .Select(s => new StageSelectionItem(s.Id, s.Name, s.Description, s.AccessCodeHash != null && s.AccessCodeHash != ""))
            .ToListAsync();

    public async Task<StageAccessCodeViewModel?> GetAccessCodeInfoAsync(int stageId)
    {
        var stage = await _db.Stages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == stageId);
        if (stage is null) return null;

        return new StageAccessCodeViewModel
        {
            Id = stage.Id,
            StageName = stage.Name,
            RequiresCode = !string.IsNullOrEmpty(stage.AccessCodeHash)
        };
    }

    public async Task<ServiceResult> SetStageAccessCodeAsync(int stageId, string? code)
    {
        var stage = await _db.Stages.FirstOrDefaultAsync(s => s.Id == stageId);
        if (stage is null) return ServiceResult.Fail("المرحلة غير موجودة.");

        if (string.IsNullOrWhiteSpace(code))
        {
            stage.AccessCodeHash = null;
            stage.AccessCodeUpdatedAt = DateTime.UtcNow;
            _audit.Add("StageAccessCodeRemoved", "Stage", stageId.ToString());
        }
        else
        {
            var trimmed = code.Trim();
            if (trimmed.Length < 4) return ServiceResult.Fail("رمز الدخول يجب ألا يقل عن 4 أحرف.");
            if (trimmed.Length > 50) return ServiceResult.Fail("رمز الدخول طويل جدًا.");

            stage.AccessCodeHash = _hasher.Hash(trimmed);
            stage.AccessCodeUpdatedAt = DateTime.UtcNow;
            _audit.Add("StageAccessCodeChanged", "Stage", stageId.ToString());
        }

        await _db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task<int> SaveAsync() => _db.SaveChangesAsync();
}
