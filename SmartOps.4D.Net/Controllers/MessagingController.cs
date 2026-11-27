using SmartOps.Web.Helpers;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
    public class MessagingController : Controller
{
    private readonly AppDBContext _context;
    private readonly UserManager<AppUser> _userManager;

    public MessagingController(
            AppDBContext context,
            UserManager<AppUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }
    public async Task<IActionResult> Index(int page = 1, int insurerPage = 1)
    {
        const int pageSize = 10;

        if (page < 1)
            page = 1;

        var query = _context.SupportTickets
            .Include(x => x.Contract)
            .Include(x => x.Customer)
            .Include(x => x.Messages)
            .Where(x => x.Status != TicketStatus.Closed);

        var totalItems = await query.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        var tickets = await query
            .OrderByDescending(x =>
                x.Status == TicketStatus.AwaitingOffice)
            .ThenByDescending(x =>
                x.Messages.Any()
                    ? x.Messages.Max(m => m.Created)
                    : x.Created)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.UnreadInsurer = await _context.InsurerMessages.CountAsync(x =>
            x.Sender == InsurerMessageSender.Insurer && !x.ReadByCompany);

        var insurerMessages = await _context.InsurerMessages
            .Include(x => x.Contract)
                .ThenInclude(c => c.Customer)
            .Include(x => x.Insurer)
            .Include(x => x.SentByUser)
            .OrderByDescending(x =>
                x.Sender == InsurerMessageSender.Insurer &&
                !x.ReadByCompany)
            .ThenByDescending(x => x.Created)
            .ThenByDescending(x => x.Id)
            .ToRecordPageAsync(insurerPage, ViewData, "Insurer");

        var model = new MessagingViewModel
        {
            CustomerMessages = tickets,
            InsurerMessages = insurerMessages
        };

        ViewBag.PageNumber = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalItems = totalItems;

        return View(model);
    }

    public async Task<IActionResult> Closed(int page = 1)
    {
        const int pageSize = 10;
        if (page < 1)
            page = 1;

        var query = _context.SupportTickets
            .Include(x => x.Contract)
            .Include(x => x.Customer)
            .Include(x => x.Messages)
            .Where(x => x.Status == TicketStatus.Closed);

        var totalItems = await query.CountAsync();

        var totalPages = (int)Math.Ceiling(
            totalItems / (double)pageSize);

        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        var tickets = await query
            .OrderByDescending(x => x.Closed ?? x.Created)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.PageNumber = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalItems = totalItems;

        return View(tickets);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(int id)
    {
        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ticket == null)
            return NotFound();

        ticket.Status = TicketStatus.AwaitingOffice;
        ticket.Closed = null;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var supportTicket = await _context.SupportTickets
            .Include(x => x.Contract)
            .Include(x => x.Customer)
            .Include(x => x.Messages)
            .ThenInclude(m => m.SentByUser)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (supportTicket == null)
            return NotFound();

        return View(supportTicket);
    }

    // GET: SUPPORTTICKETS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: SUPPORTTICKETS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,ContractId,Contract,CustomerId,Customer,Subject,Status,Created,Closed,Messages,TenantId")] SupportTicket supportticket)
    {
        if (ModelState.IsValid)
        {
            _context.Add(supportticket);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(supportticket);
    }

    // GET: SUPPORTTICKETS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var supportticket = await _context.SupportTickets.FindAsync(id);
        if (supportticket == null)
        {
            return NotFound();
        }
        return View(supportticket);
    }

    // POST: SUPPORTTICKETS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,ContractId,Contract,CustomerId,Customer,Subject,Status,Created,Closed,Messages,TenantId")] SupportTicket supportticket)
    {
        if (id != supportticket.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(supportticket);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SupportTicketExists(supportticket.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(supportticket);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id)
    {
        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ticket == null)
            return NotFound();

        ticket.Status = TicketStatus.Closed;
        ticket.Closed = DateTime.Now;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: SUPPORTTICKETS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var supportticket = await _context.SupportTickets
            .FirstOrDefaultAsync(m => m.Id == id);
        if (supportticket == null)
        {
            return NotFound();
        }

        return View(supportticket);
    }

    // POST: SUPPORTTICKETS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var supportticket = await _context.SupportTickets.FindAsync(id);
        if (supportticket != null)
        {
            _context.SupportTickets.Remove(supportticket);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool SupportTicketExists(int? id)
    {
        return _context.SupportTickets.Any(e => e.Id == id);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int id, string message)
    {
        var user = await _userManager.GetUserAsync(User);

        if (string.IsNullOrWhiteSpace(message))
            return RedirectToAction(nameof(Details), new { id });

        var ticket = await _context.SupportTickets
            .Include(x => x.Messages)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (ticket == null)
            return NotFound();

        var reply = new SupportTicketMessage
        {
            SupportTicketId = ticket.Id,
            Message = message.Trim(),
            Sender = TicketMessageSender.HeadOffice,
            Created = DateTime.UtcNow,

            ReadByOffice = true,
            ReadByCustomer = false,

            SentByUserId = user?.Id,

            TenantId = ticket.TenantId
        };

        _context.SupportTicketMessages.Add(reply);

        ticket.Status = TicketStatus.AwaitingCustomer;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id });
    }

}
