using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
public class GroupsController : Controller
{
    private readonly ICatalogService _catalog;

    public GroupsController(ICatalogService catalog) => _catalog = catalog;

    public async Task<IActionResult> Index(int? stageId)
    {
        ViewBag.Stages = await _catalog.GetStageOptionsAsync(includeInactive: true);
        ViewBag.StageId = stageId;
        return View(await _catalog.ListGroupsAsync(stageId));
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? stageId)
    {
        var model = new GroupFormViewModel { StageId = stageId };
        model.Stages = await _catalog.GetStageOptionsAsync(stageId);
        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _catalog.GetGroupAsync(id);
        if (group is null) return NotFound();

        var model = new GroupFormViewModel
        {
            Id = group.Id,
            Name = group.Name,
            StageId = group.StageId,
            Description = group.Description
        };
        model.Stages = await _catalog.GetStageOptionsAsync(group.StageId);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Save(GroupFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _catalog.SaveGroupAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = model.Id is null ? "تمت إضافة المجموعة." : "تم حفظ تعديلات المجموعة.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        model.Stages = await _catalog.GetStageOptionsAsync(model.StageId);
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var result = await _catalog.SetGroupActiveAsync(id, active);
        if (result.Succeeded) TempData["Success"] = active ? "تم تفعيل المجموعة." : "تم إيقاف المجموعة.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }
}
