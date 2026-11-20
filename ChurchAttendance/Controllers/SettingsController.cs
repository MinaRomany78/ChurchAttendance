using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
public class SettingsController : Controller
{
    private readonly ISettingsService _settings;
    private readonly ILogoStorageService _logo;

    public SettingsController(ISettingsService settings, ILogoStorageService logo)
    {
        _settings = settings;
        _logo = logo;
    }

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _settings.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Save(SettingsViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _settings.SaveAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "تم حفظ الإعدادات.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        model.LogoPath = await _settings.GetAsync(Models.SettingKeys.LogoPath);
        return View("Index", model);
    }

    [HttpPost]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> UploadLogo(IFormFile? logo)
    {
        var result = await _logo.SaveAsync(logo);
        if (result.Succeeded) TempData["Success"] = "تم تحديث الشعار.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveLogo()
    {
        await _logo.RemoveAsync();
        TempData["Success"] = "تم حذف الشعار.";
        return RedirectToAction(nameof(Index));
    }
}
