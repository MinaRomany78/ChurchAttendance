using ChurchAttendance.Models;
using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IRewardService
{
    Task<List<RewardListItem>> ListAsync(bool includeInactive);
    Task<List<RewardOption>> GetActiveOptionsAsync();
    Task<Reward?> GetAsync(int id);
    Task<ServiceResult> SaveAsync(RewardFormViewModel model);
    Task<ServiceResult> SetActiveAsync(int id, bool active);

    /// <summary>Validates a redemption and returns the data for the confirmation screen. Writes nothing.</summary>
    Task<ServiceResult<RedeemPreviewViewModel>> PreviewRedeemAsync(int studentId, int rewardId, string? notes, int? enforceStageId = null);

    /// <summary>Re-validates and records the redemption + point deduction + stock change in ONE transaction.</summary>
    Task<ServiceResult<int>> RedeemAsync(int studentId, int rewardId, string? notes, string userId, int? enforceStageId = null);

    Task<ServiceResult> ApproveAsync(int redemptionId, string userId);
    Task<ServiceResult> DeliverAsync(int redemptionId, string userId);

    /// <summary>Cancels a not-yet-delivered redemption: refunds the points and returns the stock.</summary>
    Task<ServiceResult> CancelAsync(int redemptionId, string userId);

    Task<PagedResult<RedemptionListItem>> SearchRedemptionsAsync(RedemptionListQuery query);
}
