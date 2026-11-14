using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;

namespace SmartOps.Web.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class RoleManagerController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        public RoleManagerController(RoleManager<IdentityRole> roleManager)
        {
            _roleManager = roleManager;
        }
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Index(int page = 1)
        {
            var roles = await _roleManager.Roles.Where(r => StaffRoles.All.Contains(r.Name!))
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .ToRecordPageAsync(page, ViewData);

            return View(roles);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddRole(string roleName) => Forbid();
    }
}
