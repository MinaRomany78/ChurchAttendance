using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

/// <summary>Any signed-in user (the global fallback policy requires login) may change their own password.</summary>
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly IAuditService _audit;

    public ProfileController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, IAuditService audit)
    {
        _users = users;
        _signIn = signIn;
        _audit = audit;
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        // Changing the password rotates the security stamp; refresh the cookie so this session stays signed in.
        await _signIn.RefreshSignInAsync(user);
        await _audit.LogAsync("PasswordChanged", "User", user.Id);

        TempData["Success"] = "تم تغيير كلمة المرور.";
        return RedirectToAction("Index", "Home");
    }
}
