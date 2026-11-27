using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Web.Helpers;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = StaffRoles.ManageUsers)]
public class UserRolesController : Controller
{
    public IActionResult Index(int page = 1) => RedirectToAction("Index", "Users", new { page });
    public IActionResult Manage(string userId) => RedirectToAction("Edit", "Users", new { id = userId });
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Manage(List<Models.ManageUserRolesViewModel> model, string userId) => Forbid();
}
