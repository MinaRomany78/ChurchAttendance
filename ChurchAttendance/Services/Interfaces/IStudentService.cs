using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IStudentService
{
    Task<PagedResult<StudentListItem>> SearchAsync(StudentListQuery query);
    Task<StudentFormViewModel?> GetForEditAsync(int id);
    Task<StudentDetailsViewModel?> GetDetailsAsync(int id, bool includeQr);
    Task<BadgeViewModel?> GetBadgeAsync(int id);

    Task<ServiceResult<int>> CreateAsync(StudentFormViewModel model);
    Task<ServiceResult> UpdateAsync(StudentFormViewModel model);
    Task<ServiceResult> SetActiveAsync(int id, bool active);
    Task<ServiceResult> RegenerateQrAsync(int id);

    /// <summary>Small student list used by the student picker (name/code search, active students only).</summary>
    Task<List<StudentLookupItem>> SearchLookupAsync(string term, int? stageId = null);
    Task<StudentLookupItem?> GetLookupAsync(int id);
}
