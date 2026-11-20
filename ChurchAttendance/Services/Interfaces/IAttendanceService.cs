using ChurchAttendance.Models;
using ChurchAttendance.ViewModels;

namespace ChurchAttendance.Services.Interfaces;

public interface IAttendanceService
{
    Task<PagedResult<SessionListItem>> SearchSessionsAsync(SessionListQuery query);
    Task<SessionDetailsViewModel?> GetSessionAsync(int id);

    Task<ServiceResult<int>> CreateSessionAsync(SessionFormViewModel model, string userId);
    Task<ServiceResult> CloseSessionAsync(int id, string userId);
    Task<ServiceResult> ReopenSessionAsync(int id);

    /// <summary>
    /// Registers attendance either by identifier (QR token / barcode / student code) or by studentId (manual).
    /// Success is only reported after the record has been saved.
    /// </summary>
    Task<RegisterOutcome> RegisterAsync(int sessionId, string? identifier, int? studentId,
                                        RegistrationMethod method, string userId);

    Task<List<StudentSearchResult>> SearchStudentsAsync(int sessionId, string term);
    Task<ServiceResult> CorrectAsync(int recordId, AttendanceStatus status);
    Task<bool> AllowExcusedAsync();
}
