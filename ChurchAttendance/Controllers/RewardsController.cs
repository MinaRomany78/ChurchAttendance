using System.Security.Claims;
using ChurchAttendance.Security;
using ChurchAttendance.Services;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChurchAttendance.Controllers;

[Authorize(Policy = AppPolicies.ViewData)]
public class RewardsController : Controller
{
    private readonly IRewardService _rewards;
    private readonly IStudentService _students;
    private readonly IAuthorizationService _auth;
    private readonly ICurrentStageAccessor _stage;

    public RewardsController(IRewardService rewards, IStudentService students, IAuthorizationService auth,
                             ICurrentStageAccessor stage)
    {
        _rewards = rewards;
        _students = students;
        _auth = auth;
        _stage = stage;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<bool> AllowedAsync(string policy) => (await _auth.AuthorizeAsync(User, policy)).Succeeded;

    // ---------------------------------------------------------------- catalog

    public async Task<IActionResult> Index()
    {
        var canManage = await AllowedAsync(AppPolicies.ManageRewards);
        return View(new RewardIndexViewModel
        {
            Rewards = await _rewards.ListAsync(includeInactive: canManage),
            CanManage = canManage,
            CanRedeem = await AllowedAsync(AppPolicies.RedeemRewards)
        });
    }

    [Authorize(Policy = AppPolicies.ManageRewards)]
    [HttpGet]
    public IActionResult Create() => View("Form", new RewardFormViewModel());

    [Authorize(Policy = AppPolicies.ManageRewards)]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var reward = await _rewards.GetAsync(id);
        if (reward is null) return NotFound();

        return View("Form", new RewardFormViewModel
        {
            Id = reward.Id,
            Name = reward.Name,
            Description = reward.Description,
            RequiredPoints = reward.RequiredPoints,
            StockQuantity = reward.StockQuantity
        });
    }

    [Authorize(Policy = AppPolicies.ManageRewards)]
    [HttpPost]
    public async Task<IActionResult> Save(RewardFormViewModel model)
    {
        if (!ModelState.IsValid) return View("Form", model);

        var result = await _rewards.SaveAsync(model);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }

        TempData["Success"] = model.Id is null ? "تمت إضافة المكافأة." : "تم حفظ تعديلات المكافأة.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = AppPolicies.ManageRewards)]
    [HttpPost]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var result = await _rewards.SetActiveAsync(id, active);
        if (result.Succeeded) TempData["Success"] = active ? "تم تفعيل المكافأة." : "تم إيقاف المكافأة.";
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Index));
    }

    // ---------------------------------------------------------------- redeem: form -> confirmation -> execute

    [Authorize(Policy = AppPolicies.RedeemRewards)]
    [RequireStageSelected]
    [HttpGet]
    public async Task<IActionResult> Redeem(int? studentId, int? rewardId)
    {
        var model = new RedeemFormViewModel { StudentId = studentId, RewardId = rewardId };
        await FillRedeemAsync(model);
        return View(model);
    }

    /// <summary>Step 2: validate everything and show the confirmation screen. Nothing is saved here.</summary>
    [Authorize(Policy = AppPolicies.RedeemRewards)]
    [RequireStageSelected]
    [HttpPost]
    public async Task<IActionResult> Preview(RedeemFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var preview = await _rewards.PreviewRedeemAsync(model.StudentId!.Value, model.RewardId!.Value, model.Notes, _stage.StageId);
            if (preview.Succeeded) return View("ConfirmRedeem", preview.Value);

            ModelState.AddModelError(string.Empty, preview.Error!);
        }

        await FillRedeemAsync(model);
        return View("Redeem", model);
    }

    /// <summary>Step 3: the real, transactional redemption (re-validates inside the transaction).</summary>
    [Authorize(Policy = AppPolicies.RedeemRewards)]
    [RequireStageSelected]
    [HttpPost]
    public async Task<IActionResult> Execute(int studentId, int rewardId, string? notes)
    {
        var result = await _rewards.RedeemAsync(studentId, rewardId, notes, UserId, _stage.StageId);
        if (result.Succeeded)
        {
            TempData["Success"] = "تم تسجيل الاستبدال وخصم النقاط بنجاح.";
            return RedirectToAction(nameof(Redemptions));
        }

        TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Redeem), new { studentId, rewardId });
    }

    private async Task FillRedeemAsync(RedeemFormViewModel model)
    {
        model.Rewards = await _rewards.GetActiveOptionsAsync();

        if (model.StudentId is int id)
        {
            var student = await _students.GetLookupAsync(id);
            if (student is not null)
            {
                model.StudentLabel = $"{student.Name} — {student.Code}";
                model.StudentBalance = student.Balance;
            }
        }
    }

    // ---------------------------------------------------------------- redemptions list + status changes

    public async Task<IActionResult> Redemptions([FromQuery] RedemptionListQuery query)
    {
        return View(new RedemptionIndexViewModel
        {
            Query = query,
            Result = await _rewards.SearchRedemptionsAsync(query),
            CanManage = await AllowedAsync(AppPolicies.RedeemRewards)
        });
    }

    [Authorize(Policy = AppPolicies.RedeemRewards)]
    [HttpPost]
    public async Task<IActionResult> Approve(int id) =>
        Report(await _rewards.ApproveAsync(id, UserId), "تمت الموافقة على الطلب.");

    [Authorize(Policy = AppPolicies.RedeemRewards)]
    [HttpPost]
    public async Task<IActionResult> Deliver(int id) =>
        Report(await _rewards.DeliverAsync(id, UserId), "تم تسجيل تسليم المكافأة.");

    [Authorize(Policy = AppPolicies.RedeemRewards)]
    [HttpPost]
    public async Task<IActionResult> Cancel(int id) =>
        Report(await _rewards.CancelAsync(id, UserId), "تم إلغاء الاستبدال وإرجاع النقاط للطالب.");

    private IActionResult Report(ServiceResult result, string successMessage)
    {
        if (result.Succeeded) TempData["Success"] = successMessage;
        else TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Redemptions));
    }
}
