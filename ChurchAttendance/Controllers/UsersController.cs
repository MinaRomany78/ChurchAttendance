using System.Security.Claims;
using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
public class UsersController : Controller
{
    private readonly IUserAdminService _users;

    public UsersController(IUserAdminService users) => _users = users;

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index() => View(await _users.ListAsync(CurrentUserId));

    [HttpGet]
    public IActionResult Create() => View("Form", new UserFormViewModel { Role = AppRoles.AttendanceOfficer });

    [HttpPost]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _users.CreateAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "تمت إضافة المستخدم.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        return View("Form", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var model = await _users.GetAsync(id, CurrentUserId);
        return model is null ? NotFound() : View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model)
    {
        model.Id = id;

        // Password fields do not exist on the edit form; ignore any that were sent.
        ModelState.Remove(nameof(model.Password));
        ModelState.Remove(nameof(model.ConfirmPassword));

        if (ModelState.IsValid)
        {
            var result = await _users.UpdateAsync(model, CurrentUserId);
            if (result.Succeeded)
            {
                TempData["Success"] = "تم حفظ بيانات المستخدم.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        model.IsCurrentUser = id == CurrentUserId;
        return View("Form", model);
    }

    [HttpPost]
    public async Task<IActionResult> Toggle(string id, bool active)
    {
        var result = await _users.SetActiveAsync(id, active, CurrentUserId);
        if (result.Succeeded) TempData["Success"] = active ? "تم تفعيل الحساب." : "تم إيقاف الحساب.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var model = await _users.GetResetAsync(id);
        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await _users.ResetPasswordAsync(model);
            if (result.Succeeded)
            {
                TempData["Success"] = "تم تعيين كلمة المرور الجديدة. سلّمها للمستخدم بشكل آمن.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        var info = await _users.GetResetAsync(model.Id);
        model.DisplayName = info?.DisplayName ?? string.Empty;
        return View(model);
    }
}
