using System.Security.Claims;
using ChurchAttendance.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ChurchAttendance.Security;

/// <summary>Adds the user's display name to the auth cookie so layouts can show it without a database call.</summary>
public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string FullNameClaim = "FullName";

    public AppClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var name = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? string.Empty : user.FullName;
        identity.AddClaim(new Claim(FullNameClaim, name));
        return identity;
    }
}
