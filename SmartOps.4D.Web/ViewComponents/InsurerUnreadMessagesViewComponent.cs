using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;

namespace SmartOps.Web.ViewComponents
{
    public class InsurerUnreadMessagesViewComponent : ViewComponent
    {
        private readonly AppDBContext _context;
        private readonly UserManager<AppUser> _userManager;

        public InsurerUnreadMessagesViewComponent(
            AppDBContext context,
            UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = _userManager.GetUserId(
                HttpContext.User);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user?.InsurerId == null)
                return View(0);

            var count = await _context.InsurerMessages
                .IgnoreQueryFilters()
                .CountAsync(x =>
                    x.InsurerId == user.InsurerId &&
                    x.Sender == InsurerMessageSender.Company &&
                    !x.ReadByInsurer);

            return View(count);
        }
    }
}