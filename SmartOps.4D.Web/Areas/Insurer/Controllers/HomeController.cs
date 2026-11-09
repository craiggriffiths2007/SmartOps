using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;

namespace SmartOps.Web.Areas.Insurer.Controllers
{
    [Area("Insurer")]
    [Authorize(Roles = "Insurer")]
    public class HomeController : Controller
    {
        private readonly AppDBContext _context;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(
            AppDBContext context,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var user = await _userManager.Users
                .Include(u => u.Insurer)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.InsurerId == null ||
                user.Insurer == null)
            {
                return Forbid();
            }

            ViewBag.InsurerName = user.Insurer.Name;

            var contracts = await _context.Contract
                .IgnoreQueryFilters()
                .Where(c => c.InsurerId == user.InsurerId)
                .ToListAsync();

            ViewBag.ContractCount = contracts.Count;

            return View();
        }
    }
}