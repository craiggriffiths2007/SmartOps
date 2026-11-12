using SmartOps.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace SmartOps.Web.Services;

// Applied to password, external and two-factor Identity sign-in flows.
public sealed class ApprovalSignInManager : SignInManager<AppUser>
{
    public ApprovalSignInManager(UserManager<AppUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<AppUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<AppUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<AppUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor,
            logger, schemes, confirmation) { }

    public override async Task<bool> CanSignInAsync(AppUser user)
    {
        var services = Context.RequestServices;
        if (!user.IsApproved) return false;
        if (user.TenantId is int tenantId && !await UserManager.IsInRoleAsync(user, "SuperAdmin"))
        {
            var db = services.GetRequiredService<AppDBContext>();
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(
                db.Tenant, t => t.Id == tenantId && t.IsActive)) return false;
        }
        return await base.CanSignInAsync(user);
    }
}
