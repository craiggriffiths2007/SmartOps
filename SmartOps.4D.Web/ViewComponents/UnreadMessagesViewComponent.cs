using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;

namespace SmartOps.Web.ViewComponents
{
    public class UnreadMessagesViewComponent : ViewComponent
    {
        private readonly AppDBContext _context;

        public UnreadMessagesViewComponent(AppDBContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var customerMessages = await _context.SupportTickets
                .CountAsync(t =>
                    t.Status == TicketStatus.AwaitingOffice);

            var insurerMessages = await _context.InsurerMessages
                .CountAsync(m =>
                    m.Sender == InsurerMessageSender.Insurer &&
                    !m.ReadByCompany);

            var total = customerMessages + insurerMessages;

            return View(total);
        }
    }
}