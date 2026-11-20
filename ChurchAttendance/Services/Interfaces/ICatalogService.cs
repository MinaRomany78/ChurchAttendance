using ChurchAttendance.Models;
using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

/// <summary>Stages and groups.</summary>
public interface ICatalogService
{
    Task<List<StageListItem>> ListStagesAsync();
    Task<Stage?> GetStageAsync(int id);
    Task<ServiceResult> SaveStageAsync(StageFormViewModel model);
    Task<ServiceResult> SetStageActiveAsync(int id, bool active);

    Task<List<GroupListItem>> ListGroupsAsync(int? stageId);
    Task<StudentGroup?> GetGroupAsync(int id);
    Task<ServiceResult> SaveGroupAsync(GroupFormViewModel model);
    Task<ServiceResult> SetGroupActiveAsync(int id, bool active);

    Task<List<StageOption>> GetStageOptionsAsync(int? includeId = null, bool includeInactive = false);
    Task<List<GroupOption>> GetGroupOptionsAsync(int? includeId = null, bool includeInactive = false, int? stageId = null);

    /// <summary>Active stages for the post-login selection page, with whether each needs an access code (never the code itself).</summary>
    Task<List<StageSelectionItem>> GetSelectionListAsync();

    Task<StageAccessCodeViewModel?> GetAccessCodeInfoAsync(int stageId);

    /// <summary>Sets a new access code, or removes the requirement when code is null/blank.</summary>
    Task<ServiceResult> SetStageAccessCodeAsync(int stageId, string? code);
}
