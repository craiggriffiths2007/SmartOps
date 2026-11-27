using System.ComponentModel.DataAnnotations;
using SmartOps.Web.Bridge;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SmartOps.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ChangePasswordModel(AccountService account) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class InputModel
    {
        [Required, StringLength(64, MinimumLength = 12), DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = "";
    }

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            await account.ChangePassword(Input.NewPassword);
        }
        catch (Exception error) when (error is AccountValidationException or HttpRequestException)
        {
            ModelState.AddModelError(string.Empty, error.Message);
            return Page();
        }

        // 4D revokes the account's sessions after a successful password change.
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return LocalRedirect("/Account/Login?passwordChanged=true");
    }
}
