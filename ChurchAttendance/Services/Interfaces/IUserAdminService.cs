using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IUserAdminService
{
    Task<List<UserListItem>> ListAsync(string currentUserId);
    Task<UserFormViewModel?> GetAsync(string id, string currentUserId);
    Task<ResetPasswordViewModel?> GetResetAsync(string id);

    Task<ServiceResult> CreateAsync(UserFormViewModel model);
    Task<ServiceResult> UpdateAsync(UserFormViewModel model, string currentUserId);
    Task<ServiceResult> SetActiveAsync(string id, bool active, string currentUserId);
    Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model);

    Task<Dictionary<string, int>> CountUsersByRoleAsync();
}
