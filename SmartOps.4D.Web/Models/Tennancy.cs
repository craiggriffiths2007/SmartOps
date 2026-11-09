using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SmartOps.Web.Data;
using System.ComponentModel;
using System.Security.Claims;

namespace SmartOps.Web.Models
{

 

    public class Tenant
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        // Short identifier, e.g. Billy Bob Windows
        public string Code { get; set; } = "";

        public bool IsActive { get; set; } = true;

        public DateTime Created { get; set; } = DateTime.UtcNow;


        // Company details
        public string? Address1 { get; set; }

        public string? Address2 { get; set; }

        public string? Town { get; set; }

        public string? County { get; set; }

        public string? Postcode { get; set; }

        public string? Telephone { get; set; }

        public string? Email { get; set; }

        public string? Website { get; set; }


        // Branding
        public string Theme { get; set; } = "Midnight";

        public byte[]? Logo { get; set; }

        public DateTime? LogoUpdated { get; set; }


        // Subscription
        public SubscriptionPlan SubscriptionPlan { get; set; }

        public DateTime? SubscriptionStarted { get; set; }

        public DateTime? SubscriptionExpires { get; set; }

        public bool AutoRenew { get; set; }
    }



    public interface ITenantContext
    {
        int? TenantId { get; }

        string? TenantName { get; }

        string? TenantCode { get; }
    }

    public class TenantContext : ITenantContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantContext(
            IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? TenantId
        {
            get
            {
                var user =
                    _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return null;

                var value =
                    user.FindFirst("TenantId")?.Value;

                if (int.TryParse(value, out int tenantId))
                    return tenantId;

                return null;
            }
        }

        public string? TenantName
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return null;

                return user.FindFirst("TenantName")?.Value;
            }
        }

         public string? TenantCode
        {
            get
            {
                var user =
                    _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                    return null;

                return user.FindFirst("TenantCode")?.Value;
            }
        }


    }
    
    public class AppUserClaimsPrincipalFactory
        : UserClaimsPrincipalFactory<AppUser, IdentityRole>
    {
        private readonly AppDBContext _context;
        public AppUserClaimsPrincipalFactory(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            AppDBContext context)
            : base(
                userManager,
                roleManager,
                optionsAccessor)
        {
            _context = context;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity =
                await base.GenerateClaimsAsync(user);

            identity.AddClaim(
                    new Claim(
                        "FirstName",
                        user.FirstName ?? ""));

            identity.AddClaim(
                    new Claim(
                        "ProfilePictureVersion",
                        user.ProfilePictureUpdated?.Ticks.ToString() ?? "0"));

            if (user.TenantId.HasValue)
            {
                identity.AddClaim(
                    new Claim(
                        "TenantId",
                        user.TenantId.Value.ToString()));

                var tenant = await _context.Tenant
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(t =>
                        t.Id == user.TenantId.Value);

                if (tenant != null)
                {
                    identity.AddClaim(
                        new Claim(
                            "TenantName",
                            tenant.Name));
                }
            }

            return identity;
        }
    }
}
