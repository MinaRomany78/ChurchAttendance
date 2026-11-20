using System.Security.Claims;
using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.ViewData)]
[RequireStageSelected]
public class PointsController : Controller
{
    private readonly IPointsService _points;
    private readonly IStudentService _students;
    private readonly IAuthorizationService _auth;
    private readonly ICurrentStageAccessor _stage;

    public PointsController(IPointsService points, IStudentService students, IAuthorizationService auth,
                            ICurrentStageAccessor stage)
    {
        _points = points;
        _students = students;
        _auth = auth;
        _stage = stage;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------------------------------------------------------------- ledger

    public async Task<IActionResult> Index([FromQuery] PointListQuery query)
    {
        var model = await _points.SearchAsync(query);
        model.CanManage = (await _auth.AuthorizeAsync(User, AppPolicies.ManagePoints)).Succeeded;
        return View(model);
    }

    // ---------------------------------------------------------------- award / deduct

    [Authorize(Policy = AppPolicies.ManagePoints)]
    [HttpGet]
    public async Task<IActionResult> Award(int? studentId) => await BuildFormAsync(false, studentId);

    [Authorize(Policy = AppPolicies.ManagePoints)]
    [HttpPost]
    public async Task<IActionResult> Award(PointFormViewModel model) => await SubmitAsync(model, false);

    [Authorize(Policy = AppPolicies.ManagePoints)]
    [HttpGet]
    public async Task<IActionResult> Deduct(int? studentId) => await BuildFormAsync(true, studentId);

    [Authorize(Policy = AppPolicies.ManagePoints)]
    [HttpPost]
    public async Task<IActionResult> Deduct(PointFormViewModel model) => await SubmitAsync(model, true);

    private async Task<IActionResult> BuildFormAsync(bool deduction, int? studentId)
    {
        var model = new PointFormViewModel { StudentId = studentId };
        await FillAsync(model, deduction);
        return View("Form", model);
    }

    private async Task<IActionResult> SubmitAsync(PointFormViewModel model, bool deduction)
    {
        if (ModelState.IsValid)
        {
            var result = deduction
                ? await _points.DeductAsync(model, UserId, _stage.StageId)
                : await _points.AwardAsync(model, UserId, _stage.StageId);

            if (result.Succeeded)
            {
                TempData["Success"] = deduction ? "تم خصم النقاط وتسجيل العملية." : "تمت إضافة النقاط بنجاح.";
                return RedirectToAction(nameof(Index), new { studentId = model.StudentId });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await FillAsync(model, deduction);
        return View("Form", model);
    }

    private async Task FillAsync(PointFormViewModel model, bool deduction)
    {
        model.IsDeduction = deduction;
        model.Reasons = await _points.GetReasonOptionsAsync(deduction);

        if (model.StudentId is int id)
        {
            var student = await _students.GetLookupAsync(id);
            if (student is not null)
            {
                model.StudentLabel = $"{student.Name} — {student.Code}";
                model.StudentBalance = student.Balance;
            }
        }
    }

    // ---------------------------------------------------------------- reasons (admin)

    [Authorize(Policy = AppPolicies.AdminOnly)]
    public async Task<IActionResult> Reasons() => View(await _points.ListReasonsAsync());

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet]
    public IActionResult ReasonCreate() => View("ReasonForm", new PointReasonFormViewModel());

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpGet]
    public async Task<IActionResult> ReasonEdit(int id)
    {
        var reason = await _points.GetReasonAsync(id);
        if (reason is null) return NotFound();

        return View("ReasonForm", new PointReasonFormViewModel
        {
            Id = reason.Id,
            Name = reason.Name,
            DefaultPoints = reason.DefaultPoints,
            IsDeduction = reason.IsDeduction
        });
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpPost]
    public async Task<IActionResult> ReasonSave(PointReasonFormViewModel model)
    {
        if (!ModelState.IsValid) return View("ReasonForm", model);

        var result = await _points.SaveReasonAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("ReasonForm", model);
        }

        TempData["Success"] = "تم حفظ السبب.";
        return RedirectToAction(nameof(Reasons));
    }

    [Authorize(Policy = AppPolicies.AdminOnly)]
    [HttpPost]
    public async Task<IActionResult> ReasonToggle(int id, bool active)
    {
        var result = await _points.SetReasonActiveAsync(id, active);
        if (result.Succeeded) TempData["Success"] = active ? "تم تفعيل السبب." : "تم إيقاف السبب.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Reasons));
    }
}
