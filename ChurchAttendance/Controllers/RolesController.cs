using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

/// <summary>
/// Read-only overview. The four roles and what each may do are defined in code (Security/AppPolicies.cs)
/// so they can be enforced and reviewed; users are assigned to roles from the Users page.
/// </summary>
[Authorize(Policy = AppPolicies.AdminOnly)]
public class RolesController : Controller
{
    private readonly IUserAdminService _users;

    public RolesController(IUserAdminService users) => _users = users;

    public async Task<IActionResult> Index()
    {
        var counts = await _users.CountUsersByRoleAsync();

        return View(new RoleMatrixViewModel
        {
            Roles = AppRoles.All.Select(r => (r, AppRoles.DisplayName(r), counts.GetValueOrDefault(r))).ToList(),
            Permissions = AppPolicies.Permissions.Select(p => (p.Label, p.Roles)).ToList()
        });
    }
}
