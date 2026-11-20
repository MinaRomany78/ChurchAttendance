using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChurchAttendance.Services
{
  
    public class GroupPointsService : IGroupPointsService
    {
        private readonly ApplicationDbContext _db;
        private readonly IAuditService _auditService;

        public GroupPointsService(ApplicationDbContext db, IAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        public async Task<ServiceResult<GroupPointTransaction>> AddGroupPointsAsync(int groupId, int points, string reason, string userId, string? notes = null)
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var group = await _db.StudentGroups.FindAsync(groupId);
                if (group == null || !group.IsActive)
                    return ServiceResult<GroupPointTransaction>.Fail("المجموعة غير موجودة أو غير نشطة.");

                var pointTx = new GroupPointTransaction
                {
                    GroupId = groupId,
                    Points = points,
                    Reason = reason,
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Notes = notes
                };

                _db.GroupPointTransactions.Add(pointTx);
                await _db.SaveChangesAsync();

                _auditService.Add("GroupPointsAdded", "StudentGroup", groupId.ToString(), null, new { Points = points, Reason = reason });
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                return ServiceResult<GroupPointTransaction>.Ok(pointTx);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult<GroupPointTransaction>.Fail($"حدث خطأ أثناء معالجة النقاط: {ex.Message}");
            }
        }

        public async Task<List<GroupPointTransaction>> GetGroupHistoryAsync(int groupId)
        {
            return await _db.GroupPointTransactions
                .Include(t => t.CreatedByUser)
                .Where(t => t.GroupId == groupId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> GetGroupBalanceAsync(int groupId)
        {
            return await _db.GroupPointTransactions
                .Where(t => t.GroupId == groupId)
                .SumAsync(t => t.Points);
        }
    }
}