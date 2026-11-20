using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.AdminOnly)]
public class AuditController : Controller
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    public async Task<IActionResult> Index([FromQuery] AuditQuery query) => View(await _audit.SearchAsync(query));
}
