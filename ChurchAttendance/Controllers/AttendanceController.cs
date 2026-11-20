using System.Security.Claims;
using ChurchAttendance.Models;
using ChurchAttendance.Security;
using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.ViewData)]
[RequireStageSelected]
public class AttendanceController : Controller
{
    private readonly IAttendanceService _attendance;
    private readonly ICatalogService _catalog;
    private readonly IAuthorizationService _auth;
    private readonly IAppClock _clock;
    private readonly ICurrentStageAccessor _stage;

    public AttendanceController(IAttendanceService attendance, ICatalogService catalog,
                                IAuthorizationService auth, IAppClock clock, ICurrentStageAccessor stage)
    {
        _attendance = attendance;
        _catalog = catalog;
        _auth = auth;
        _clock = clock;
        _stage = stage;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<bool> AllowedAsync(string policy) => (await _auth.AuthorizeAsync(User, policy)).Succeeded;

    // ---------------------------------------------------------------- sessions list

    public async Task<IActionResult> Index([FromQuery] SessionListQuery query)
    {
        query.StageId = _stage.StageId;

        var model = new SessionIndexViewModel
        {
            Query = query,
            Result = await _attendance.SearchSessionsAsync(query),
            Stages = _stage.StageId is int sid ? new List<StageOption> { new(sid, _stage.StageName ?? string.Empty) } : new(),
            CanCreate = await AllowedAsync(AppPolicies.RegisterAttendance)
        };

        return View(model);
    }

    // ---------------------------------------------------------------- create session

    [Authorize(Policy = AppPolicies.RegisterAttendance)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var local = _clock.ToLocal(_clock.UtcNow);
        var model = new SessionFormViewModel
        {
            SessionDate = DateOnly.FromDateTime(local),
            StartTime = new TimeOnly(local.Hour, local.Minute),
            StageId = _stage.StageId
        };
        await FillOptionsAsync(model);
        return View(model);
    }

    [Authorize(Policy = AppPolicies.RegisterAttendance)]
    [HttpPost]
    public async Task<IActionResult> Create(SessionFormViewModel model)
    {
        model.StageId = _stage.StageId; // a session always belongs to the stage it is opened from

        if (ModelState.IsValid)
        {
            var result = await _attendance.CreateSessionAsync(model, CurrentUserId);
            if (result.Succeeded)
            {
                TempData["Success"] = "تم فتح جلسة الحضور.";
                return RedirectToAction(nameof(Session), new { id = result.Value });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await FillOptionsAsync(model);
        return View(model);
    }

    // ---------------------------------------------------------------- session page

    public async Task<IActionResult> Session(int id)
    {
        var model = await _attendance.GetSessionAsync(id);
        if (model is null) return NotFound();
        if (model.StageId is not null && model.StageId != _stage.StageId) return Forbid();

        model.CanRegister = await AllowedAsync(AppPolicies.RegisterAttendance);
        model.CanCorrect = await AllowedAsync(AppPolicies.CorrectAttendance);
        return View(model);
    }

    [Authorize(Policy = AppPolicies.RegisterAttendance)]
    [HttpPost]
    public async Task<IActionResult> Close(int id)
    {
        var result = await _attendance.CloseSessionAsync(id, CurrentUserId);
        if (result.Succeeded) TempData["Success"] = "تم إغلاق الجلسة وتسجيل غياب من لم يحضر.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Session), new { id });
    }

    [Authorize(Policy = AppPolicies.CorrectAttendance)]
    [HttpPost]
    public async Task<IActionResult> Reopen(int id)
    {
        var result = await _attendance.ReopenSessionAsync(id);
        if (result.Succeeded) TempData["Success"] = "تمت إعادة فتح الجلسة.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Session), new { id });
    }

    [Authorize(Policy = AppPolicies.CorrectAttendance)]
    [HttpPost]
    public async Task<IActionResult> Correct(int sessionId, int recordId, AttendanceStatus status)
    {
        var result = await _attendance.CorrectAsync(recordId, status);
        if (result.Succeeded) TempData["Success"] = "تم تعديل حالة الحضور.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Session), new { id = sessionId });
    }

    // ---------------------------------------------------------------- AJAX: scanning / manual registration

    [Authorize(Policy = AppPolicies.RegisterAttendance)]
    [HttpPost]
    public async Task<IActionResult> Register(int sessionId, string? identifier, int? studentId, RegistrationMethod method)
    {
        if (!Enum.IsDefined(method)) return BadRequest();

        var o = await _attendance.RegisterAsync(sessionId, identifier, studentId, method, CurrentUserId);

        return Json(new
        {
            success = o.Succeeded,
            duplicate = o.IsDuplicate,
            message = o.Message,
            studentName = o.StudentName,
            studentCode = o.StudentCode,
            status = o.StatusText,
            time = o.TimeText,
            registered = o.RegisteredCount
        });
    }

    [Authorize(Policy = AppPolicies.RegisterAttendance)]
    [HttpGet]
    public async Task<IActionResult> SearchStudents(int sessionId, string? term)
    {
        var results = await _attendance.SearchStudentsAsync(sessionId, term ?? string.Empty);
        return Json(results.Select(r => new { id = r.Id, name = r.Name, code = r.Code, stage = r.Stage, registered = r.Registered }));
    }

    // ---------------------------------------------------------------- helpers

    private async Task FillOptionsAsync(SessionFormViewModel model)
    {
        model.Stages = _stage.StageId is int sid ? new List<StageOption> { new(sid, _stage.StageName ?? string.Empty) } : new();
        model.Groups = await _catalog.GetGroupOptionsAsync(model.GroupId, stageId: _stage.StageId);
    }
}
