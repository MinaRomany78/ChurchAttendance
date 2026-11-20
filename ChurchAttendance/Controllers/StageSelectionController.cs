using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

/// <summary>
/// The gate every signed-in user passes through before reaching the dashboard or any stage-scoped screen
/// (students, attendance, points, rewards). Deliberately carries no [RequireStageSelected] attribute — this
/// is where a stage gets selected, not a page that needs one already.
/// </summary>
[Authorize]
public class StageSelectionController : Controller
{
    private readonly ICatalogService _catalog;
    private readonly ICurrentStageAccessor _stage;

    public StageSelectionController(ICatalogService catalog, ICurrentStageAccessor stage)
    {
        _catalog = catalog;
        _stage = stage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl)
    {
        var stages = await _catalog.GetSelectionListAsync();
        return View(new StageSelectionViewModel { Stages = stages, ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Enter(int stageId, string? code, string? returnUrl)
    {
        var ok = await _stage.TrySelectAsync(stageId, code);
        if (!ok)
        {
            TempData["Error"] = "رمز الدخول غير صحيح، أو المرحلة لم تعد متاحة.";
            return RedirectToAction(nameof(Index), new { returnUrl });
        }

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Switch()
    {
        _stage.Clear();
        return RedirectToAction(nameof(Index));
    }
}
