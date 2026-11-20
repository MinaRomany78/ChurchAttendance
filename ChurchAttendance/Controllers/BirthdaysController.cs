using System;
using System.Linq;
using System.Threading.Tasks;
using ChurchAttendance.Data;
using ChurchAttendance.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Controllers
{
    [Authorize]
    [RequireStageSelected]
    public class BirthdaysController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ICurrentStageAccessor _stageAccessor;

        public BirthdaysController(ApplicationDbContext db, ICurrentStageAccessor stageAccessor)
        {
            _db = db;
            _stageAccessor = stageAccessor;
        }

        public async Task<IActionResult> Index(int? month, int? groupId)
        {
            // تصحيح قراءة المرحلة الحالية
            int currentStageId = _stageAccessor.StageId ?? 0;
            int targetMonth = month ?? DateTime.Today.Month;

            var query = _db.Students
                .Include(s => s.Group)
                .Where(s => s.StageId == currentStageId && s.IsActive && s.DateOfBirth.HasValue)
                .Where(s => s.DateOfBirth!.Value.Month == targetMonth);

            if (groupId.HasValue && groupId > 0)
            {
                query = query.Where(s => s.GroupId == groupId.Value);
            }

            var students = await query
                .OrderBy(s => s.DateOfBirth!.Value.Day)
                .ToListAsync();

            ViewBag.SelectedMonth = targetMonth;
            ViewBag.SelectedGroupId = groupId;
            ViewBag.Groups = await _db.StudentGroups
                .Where(g => g.StageId == currentStageId && g.IsActive)
                .ToListAsync();

            return View(students);
        }
    }
}