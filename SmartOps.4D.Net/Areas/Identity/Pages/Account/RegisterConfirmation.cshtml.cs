using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartOps.Web.Data;
using SmartOps.Web.Models;

namespace SmartOps.Web.Areas.Identity.Pages.Account
{
    public class RegisterConfirmationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;

        public RegisterConfirmationModel(
            UserManager<AppUser> userManager)
        {
            _userManager = userManager;
        }

        public string Email { get; set; } = "";
        public bool AwaitingApproval { get; private set; }
        public bool AwaitingEmailConfirmation { get; private set; }

        public async Task<IActionResult> OnGetAsync(
            string email,
            string? returnUrl = null)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToPage("/Account/Login");
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return NotFound(
                    $"Unable to load user with email '{email}'.");
            }

            Email = email;
            AwaitingApproval = !user.IsApproved;
            AwaitingEmailConfirmation = _userManager.Options.SignIn.RequireConfirmedEmail && !user.EmailConfirmed;

            return Page();
        }
    }
}