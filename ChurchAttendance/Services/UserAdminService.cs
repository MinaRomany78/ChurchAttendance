using ChurchAttendance.Data;
using ChurchAttendance.Models;
using ChurchAttendance.Security;
using ChurchAttendance.Services.Interfaces;
using ChurchAttendance.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ChurchAttendance.Services;

public class UserAdminService : IUserAdminService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly IAuditService _audit;

    public UserAdminService(UserManager<ApplicationUser> users, ApplicationDbContext db, IAuditService audit)
    {
        _users = users;
        _db = db;
        _audit = audit;
    }

    public async Task<List<UserListItem>> ListAsync(string currentUserId)
    {
        var users = await _users.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();

        var roleMap = await (from ur in _db.UserRoles
                             join r in _db.Roles on ur.RoleId equals r.Id
                             select new { ur.UserId, RoleName = r.Name })
            .ToListAsync();

        return users.Select(u => new UserListItem
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email ?? u.UserName ?? string.Empty,
            RoleName = AppRoles.DisplayName(roleMap.FirstOrDefault(x => x.UserId == u.Id)?.RoleName ?? string.Empty),
            IsActive = u.IsActive,
            IsLockedOut = u.LockoutEnd is not null && u.LockoutEnd > DateTimeOffset.UtcNow,
            IsCurrentUser = u.Id == currentUserId
        }).ToList();
    }

    public async Task<UserFormViewModel?> GetAsync(string id, string currentUserId)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return null;

        var roles = await _users.GetRolesAsync(user);
        return new UserFormViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? string.Empty,
            IsActive = user.IsActive,
            IsCurrentUser = user.Id == currentUserId
        };
    }

    public async Task<ResetPasswordViewModel?> GetResetAsync(string id)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return null;

        return new ResetPasswordViewModel
        {
            Id = user.Id,
            DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? string.Empty : user.FullName
        };
    }

    public async Task<ServiceResult> CreateAsync(UserFormViewModel m)
    {
        if (!AppRoles.All.Contains(m.Role)) return ServiceResult.Fail("الدور المختار غير صالح.");
        if (string.IsNullOrWhiteSpace(m.Password)) return ServiceResult.Fail("يرجى إدخال كلمة المرور.");

        var email = m.Email.Trim();
        if (await _users.FindByEmailAsync(email) is not null) return ServiceResult.Fail("البريد الإلكتروني مستخدم بالفعل.");

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = m.FullName.Trim(),
            IsActive = m.IsActive
        };

        var created = await _users.CreateAsync(user, m.Password);
        if (!created.Succeeded) return ServiceResult.Fail(Describe(created));

        var inRole = await _users.AddToRoleAsync(user, m.Role);
        if (!inRole.Succeeded)
        {
            await _users.DeleteAsync(user); // do not leave a user without a role
            return ServiceResult.Fail(Describe(inRole));
        }

        await _audit.LogAsync("UserCreated", "User", user.Id, null, new { email, m.Role, user.IsActive });
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> UpdateAsync(UserFormViewModel m, string currentUserId)
    {
        if (m.Id is null) return ServiceResult.Fail("المستخدم غير موجود.");
        if (!AppRoles.All.Contains(m.Role)) return ServiceResult.Fail("الدور المختار غير صالح.");

        var user = await _users.FindByIdAsync(m.Id);
        if (user is null) return ServiceResult.Fail("المستخدم غير موجود.");

        var currentRoles = await _users.GetRolesAsync(user);
        var currentRole = currentRoles.FirstOrDefault();
        var roleChanged = m.Role != currentRole;
        var activeChanged = m.IsActive != user.IsActive;

        if (user.Id == currentUserId && (roleChanged || !m.IsActive))
            return ServiceResult.Fail("لا يمكنك تغيير دورك أو إيقاف حسابك بنفسك.");

        if (currentRole == AppRoles.SystemAdministrator && user.IsActive
            && (roleChanged || !m.IsActive) && !await AnotherActiveAdminExistsAsync(user.Id))
            return ServiceResult.Fail("يجب أن يبقى مدير نظام نشط واحد على الأقل.");

        var email = m.Email.Trim();
        var other = await _users.FindByEmailAsync(email);
        if (other is not null && other.Id != user.Id) return ServiceResult.Fail("البريد الإلكتروني مستخدم بالفعل.");

        var old = new { user.Email, Role = currentRole, user.IsActive };

        user.FullName = m.FullName.Trim();
        user.Email = email;
        user.UserName = email;
        user.IsActive = m.IsActive;

        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return ServiceResult.Fail(Describe(updated));

        if (roleChanged)
        {
            if (currentRoles.Count > 0) await _users.RemoveFromRolesAsync(user, currentRoles);
            var added = await _users.AddToRoleAsync(user, m.Role);
            if (!added.Succeeded) return ServiceResult.Fail(Describe(added));
        }

        // Invalidates the user's existing sessions so a new role / deactivation takes effect quickly.
        if (roleChanged || activeChanged) await _users.UpdateSecurityStampAsync(user);

        await _audit.LogAsync("UserUpdated", "User", user.Id, old, new { user.Email, m.Role, user.IsActive });
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> SetActiveAsync(string id, bool active, string currentUserId)
    {
        var user = await _users.FindByIdAsync(id);
        if (user is null) return ServiceResult.Fail("المستخدم غير موجود.");
        if (user.IsActive == active) return ServiceResult.Ok();

        if (!active)
        {
            if (user.Id == currentUserId) return ServiceResult.Fail("لا يمكنك إيقاف حسابك بنفسك.");

            if (await _users.IsInRoleAsync(user, AppRoles.SystemAdministrator)
                && !await AnotherActiveAdminExistsAsync(user.Id))
                return ServiceResult.Fail("يجب أن يبقى مدير نظام نشط واحد على الأقل.");
        }

        user.IsActive = active;
        var updated = await _users.UpdateAsync(user);
        if (!updated.Succeeded) return ServiceResult.Fail(Describe(updated));

        await _users.UpdateSecurityStampAsync(user);
        await _audit.LogAsync(active ? "UserActivated" : "UserDeactivated", "User", user.Id);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel m)
    {
        var user = await _users.FindByIdAsync(m.Id);
        if (user is null) return ServiceResult.Fail("المستخدم غير موجود.");

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, m.NewPassword);
        if (!result.Succeeded) return ServiceResult.Fail(Describe(result));

        await _users.SetLockoutEndDateAsync(user, null);
        await _users.ResetAccessFailedCountAsync(user);

        // The password itself is never written to the audit log.
        await _audit.LogAsync("UserPasswordReset", "User", user.Id);
        return ServiceResult.Ok();
    }

    public async Task<Dictionary<string, int>> CountUsersByRoleAsync()
    {
        var pairs = await (from ur in _db.UserRoles
                           join r in _db.Roles on ur.RoleId equals r.Id
                           select r.Name).ToListAsync();

        return pairs.Where(n => n is not null).GroupBy(n => n!).ToDictionary(g => g.Key, g => g.Count());
    }

    private async Task<bool> AnotherActiveAdminExistsAsync(string excludeUserId)
    {
        var admins = await _users.GetUsersInRoleAsync(AppRoles.SystemAdministrator);
        return admins.Any(u => u.IsActive && u.Id != excludeUserId);
    }

    private static string Describe(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
