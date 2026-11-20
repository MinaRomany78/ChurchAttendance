using ChurchAttendance.Models;

namespace ChurchAttendance.Services.Interfaces
{
    public interface IGroupPointsService
    {
        Task<ServiceResult<GroupPointTransaction>> AddGroupPointsAsync(int groupId, int points, string reason, string userId, string? notes = null);
        Task<List<GroupPointTransaction>> GetGroupHistoryAsync(int groupId);
        Task<int> GetGroupBalanceAsync(int groupId);
    }
}
