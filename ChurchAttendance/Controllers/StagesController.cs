using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
public class StagesController : Controller
{
    private readonly ICatalogService _catalog;

    public StagesController(ICatalogService catalog) => _catalog = catalog;

    public async Task<IActionResult> Index() => View(await _catalog.ListStagesAsync());

    [HttpGet]
    public IActionResult Create() => View("Form", new StageFormViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var stage = await _catalog.GetStageAsync(id);
        if (stage is null) return NotFound();

        return View("Form", new StageFormViewModel
        {
            Id = stage.Id,
            Name = stage.Name,
            Description = stage.Description,
            SortOrder = stage.SortOrder
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save(StageFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var result = await _catalog.SaveStageAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }

        TempData["Success"] = model.Id is null ? "تمت إضافة المرحلة." : "تم حفظ تعديلات المرحلة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var result = await _catalog.SetStageActiveAsync(id, active);
        if (result.Succeeded) TempData["Success"] = active ? "تم تفعيل المرحلة." : "تم إيقاف المرحلة.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> AccessCode(int id)
    {
        var model = await _catalog.GetAccessCodeInfoAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SetAccessCode(int id, string? code)
    {
        var result = await _catalog.SetStageAccessCodeAsync(id, code);
        if (result.Succeeded)
        {
            TempData["Success"] = string.IsNullOrWhiteSpace(code) ? "أصبحت المرحلة بلا رمز دخول." : "تم تحديث رمز الدخول.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Error"] = result.Error;
        return RedirectToAction(nameof(AccessCode), new { id });
    }
}
