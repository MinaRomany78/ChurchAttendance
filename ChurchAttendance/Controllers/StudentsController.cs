using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.ViewData)]
[RequireStageSelected]
public class StudentsController : Controller
{
    private readonly IStudentService _students;
    private readonly ICatalogService _catalog;
    private readonly IAuthorizationService _auth;
    private readonly ICurrentStageAccessor _stage;

    public StudentsController(IStudentService students, ICatalogService catalog, IAuthorizationService auth,
                              ICurrentStageAccessor stage)
    {
        _students = students;
        _catalog = catalog;
        _auth = auth;
        _stage = stage;
    }

    /// <summary>Defense in depth: even though lists are already scoped, a guessed id must not reach another stage's student.</summary>
    private IActionResult? EnsureSameStage(int studentStageId) =>
        studentStageId == _stage.StageId ? null : Forbid();

    private async Task<bool> CanManageAsync() =>
        (await _auth.AuthorizeAsync(User, AppPolicies.ManageStudents)).Succeeded;

    // ---------------------------------------------------------------- list / details

    public async Task<IActionResult> Index([FromQuery] StudentListQuery query)
    {
        query.StageId = _stage.StageId; // the stage in session is authoritative, not whatever the query string says

        var model = new StudentIndexViewModel
        {
            Query = query,
            Result = await _students.SearchAsync(query),
            Stages = _stage.StageId is int sid ? new List<StageOption> { new(sid, _stage.StageName ?? string.Empty) } : new(),
            Groups = await _catalog.GetGroupOptionsAsync(includeInactive: true, stageId: _stage.StageId),
            CanManage = await CanManageAsync()
        };

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var model = await _students.GetDetailsAsync(id, includeQr: await CanManageAsync());
        if (model is null) return NotFound();
        return EnsureSameStage(model.StageId) ?? View(model);
    }

    // ---------------------------------------------------------------- create / edit

    [Authorize(Policy = AppPolicies.ManageStudents)]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new StudentFormViewModel { StageId = _stage.StageId };
        await FillOptionsAsync(model);
        return View("Form", model);
    }

    [Authorize(Policy = AppPolicies.ManageStudents)]
    [HttpPost]
    public async Task<IActionResult> Create(StudentFormViewModel model)
    {
        model.StageId = _stage.StageId; // always the current stage, regardless of what the form posted

        if (ModelState.IsValid)
        {
            var result = await _students.CreateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "تمت إضافة الطالب بنجاح.";
                return RedirectToAction(nameof(Details), new { id = result.Value });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await FillOptionsAsync(model);
        return View("Form", model);
    }

    [Authorize(Policy = AppPolicies.ManageStudents)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _students.GetForEditAsync(id);
        if (model is null) return NotFound();
        if (EnsureSameStage(model.CurrentStageId ?? 0) is IActionResult denied) return denied;

        await FillOptionsAsync(model);
        return View("Form", model);
    }

    [Authorize(Policy = AppPolicies.ManageStudents)]
    [HttpPost]
    public async Task<IActionResult> Edit(int id, StudentFormViewModel model)
    {
        model.Id = id; // never trust an id coming from the form body
        model.StageId = _stage.StageId; // a student always stays in the stage they are being edited from

        if (ModelState.IsValid)
        {
            var result = await _students.UpdateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "تم حفظ تعديلات الطالب.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        await FillOptionsAsync(model);
        return View("Form", model);
    }

    // ---------------------------------------------------------------- state changes

    [Authorize(Policy = AppPolicies.ManageStudents)]
    [HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, string? returnTo = null)
    {
        var owner = await _students.GetLookupAsync(id);
        if (owner is null) return NotFound();
        if (EnsureSameStage(owner.StageId) is IActionResult denied0) return denied0;

        var result = await _students.SetActiveAsync(id, active);
        if (result.Succeeded) TempData["Success"] = active ? "تم تفعيل الطالب." : "تم إيقاف الطالب. سجلاته السابقة محفوظة.";
        else TempData["Error"] = result.Error;

        return returnTo == "details"
            ? RedirectToAction(nameof(Details), new { id })
            : RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = AppPolicies.ManageStudents)]
    [HttpPost]
    public async Task<IActionResult> RegenerateQr(int id)
    {
        var owner = await _students.GetLookupAsync(id);
        if (owner is null) return NotFound();
        if (EnsureSameStage(owner.StageId) is IActionResult denied0) return denied0;

        var result = await _students.RegenerateQrAsync(id);
        if (result.Succeeded) TempData["Success"] = "تم إنشاء رمز QR جديد. البطاقة القديمة لم تعد صالحة.";
        else TempData["Error"] = result.Error;

        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------------------------------------------------------------- badge

    [Authorize(Policy = AppPolicies.ManageStudents)]
    [HttpGet]
    public async Task<IActionResult> Badge(int id)
    {
        var owner = await _students.GetLookupAsync(id);
        if (owner is null) return NotFound();
        if (EnsureSameStage(owner.StageId) is IActionResult denied) return denied;

        var model = await _students.GetBadgeAsync(id);
        return model is null ? NotFound() : View(model);
    }

    // ---------------------------------------------------------------- student picker (AJAX)

    [Authorize(Policy = AppPolicies.ManagePoints)]
    [HttpGet]
    public async Task<IActionResult> Lookup(string? term)
    {
        var items = await _students.SearchLookupAsync(term ?? string.Empty, _stage.StageId);
        return Json(items.Select(i => new { id = i.Id, name = i.Name, code = i.Code, stage = i.Stage, balance = i.Balance }));
    }

    // ---------------------------------------------------------------- helpers

    private async Task FillOptionsAsync(StudentFormViewModel model)
    {
        model.Stages = _stage.StageId is int sid ? new List<StageOption> { new(sid, _stage.StageName ?? string.Empty) } : new();
        model.Groups = await _catalog.GetGroupOptionsAsync(model.GroupId, stageId: _stage.StageId);
    }
}
