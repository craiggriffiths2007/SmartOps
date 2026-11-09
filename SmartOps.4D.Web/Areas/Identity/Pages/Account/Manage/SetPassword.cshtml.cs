using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SmartOps.Web.Areas.Identity.Pages.Account.Manage;

// A 4D login always has a password. Old bookmarks must use the real change-password flow.
[Authorize]
public class SetPasswordModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("./ChangePassword");
    public IActionResult OnPost() => RedirectToPage("./ChangePassword");
}
