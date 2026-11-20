using ChurchAttendance.Models;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private const string GenericLoginError = "بيانات الدخول غير صحيحة.";

    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _audit;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService audit)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _audit = audit;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var login = model.UserNameOrEmail.Trim();
        var user = await _userManager.FindByEmailAsync(login) ?? await _userManager.FindByNameAsync(login);

        // Same message for unknown user / inactive user / wrong password so accounts cannot be enumerated.
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, GenericLoginError);
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
     user,
     model.Password,
     model.RememberMe,
     lockoutOnFailure: false);

        if (result.Succeeded)
        {
            await _audit.LogAsync(
                "LoginSucceeded",
                "User",
                user.Id,
                actorUserId: user.Id,
                actorUserName: user.UserName);

            return !string.IsNullOrEmpty(model.ReturnUrl) &&
                   Url.IsLocalUrl(model.ReturnUrl)
                ? LocalRedirect(model.ReturnUrl)
                : RedirectToAction("Index", "Home");
        }

        await _audit.LogAsync(
            "LoginFailed",
            "User",
            user.Id,
            actorUserId: user.Id,
            actorUserName: user.UserName);

        ModelState.AddModelError(
            string.Empty,
            GenericLoginError);

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            await _audit.LogAsync("Logout", "User");
        }

        await _signInManager.SignOutAsync();
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
