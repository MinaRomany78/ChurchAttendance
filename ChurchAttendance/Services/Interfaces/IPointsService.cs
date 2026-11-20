using ChurchAttendance.Models;
using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IPointsService
{
    Task<PointIndexViewModel> SearchAsync(PointListQuery query);
    Task<List<ReasonOption>> GetReasonOptionsAsync(bool deduction);

    Task<ServiceResult<int>> AwardAsync(PointFormViewModel model, string userId, int? enforceStageId = null);
    Task<ServiceResult<int>> DeductAsync(PointFormViewModel model, string userId, int? enforceStageId = null);

    Task<List<PointReasonListItem>> ListReasonsAsync();
    Task<PointReason?> GetReasonAsync(int id);
    Task<ServiceResult> SaveReasonAsync(PointReasonFormViewModel model);
    Task<ServiceResult> SetReasonActiveAsync(int id, bool active);
}
