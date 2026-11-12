using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity.UI.Services;
using SmartOps.Web.Helpers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.Services;

[Authorize(Roles = "SuperAdmin")]
public class TenantsController : Controller
{
    private readonly AppDBContext _context;
    private readonly ITenantDeletionService _tenantDeletionService;
    private readonly IEmailSender _email;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        AppDBContext context,
        ITenantDeletionService tenantDeletionService, IEmailSender email, IConfiguration configuration, ILogger<TenantsController> logger)
    {
        _context = context;
        _tenantDeletionService = tenantDeletionService;
        _email = email;
        _configuration = configuration;
        _logger = logger;
    }

    // GET: TENANTS
    public async Task<IActionResult> Index(int page = 1)
    {
        // The review action requires a userId, not a tenantId.
        var pendingApprovals = await _context.Users
            .AsNoTracking()
            .Where(u => !u.IsApproved && u.TenantId != null)
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, UserId = g.Select(u => u.Id).First() })
            .ToDictionaryAsync(x => x.TenantId, x => x.UserId);

        ViewBag.PendingApprovals = pendingApprovals;

        return View(await _context.Tenant.OrderBy(x => x.Name).ThenBy(x => x.Id).ToRecordPageAsync(page, ViewData));
    }

    // GET: TENANTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var tenant = await _context.Tenant
            .FirstOrDefaultAsync(m => m.Id == id);
        if (tenant == null)
        {
            return NotFound();
        }

        return View(tenant);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var tenant = await _context.Tenant.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();
        tenant.IsActive = false;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Company account deactivated. Company data has been retained.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id)
    {
        var tenant = await _context.Tenant.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();
        tenant.IsActive = true;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Company account reactivated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailAccountNotification(int id)
    {
        var tenant = await _context.Tenant.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();
        if (string.IsNullOrWhiteSpace(tenant.Email))
        {
            TempData["Error"] = "Set the company email address before sending an account notification.";
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            var encode = HtmlEncoder.Default;
            var status = tenant.IsActive ? "active" : "deactivated";
            var requestEmail = _configuration["EmailSettings:DataRequestEmail"]
                ?? _configuration["EmailSettings:SenderEmail"];
            var dataRequestText = string.IsNullOrWhiteSpace(requestEmail)
                ? "Please reply to this email to request a copy of your company data."
                : $"To request a copy of your company data, please email {encode.Encode(requestEmail)}.";
            await _email.SendEmailAsync(tenant.Email.Trim(), "Mart Ops account status notification",
                $"<p>Your Mart Ops account for <strong>{encode.Encode(tenant.Name)}</strong> is currently <strong>{status}</strong>.</p>"
                + $"<p>{dataRequestText}</p><p>Kind regards,<br />Mart Ops</p>");
            TempData["Success"] = $"Account notification emailed to {tenant.Email}.";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not email account notification for tenant {TenantId}", tenant.Id);
            TempData["Error"] = "The account notification could not be sent. Please check the company email address and email settings, then try again.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET: TENANTS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: TENANTS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
    [Bind("Id,Name,Code,IsActive,Created,Address1,Address2,Town,County,Postcode,Telephone,Email,Website,Logo,LogoUpdated,SubscriptionPlan,SubscriptionStarted,SubscriptionExpires,AutoRenew")] Tenant tenant,
    bool AddTestData)
    {
        if (ModelState.IsValid)
        {
            _context.Add(tenant);
            await _context.SaveChangesAsync();

            if (AddTestData)
            {
                await DbInitializer.AddTestDataAsync(
                    _context,
                    tenant.Id);
            }

            return RedirectToAction(nameof(Index));
        }

        return View(tenant);
    }

    // GET: TENANTS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var tenant = await _context.Tenant.FindAsync(id);
        if (tenant == null)
        {
            return NotFound();
        }
        return View(tenant);
    }

    // POST: TENANTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Code,IsActive,Created,Address1,Address2,Town,County,Postcode,Telephone,Email,Website,Logo,LogoUpdated,SubscriptionPlan,SubscriptionStarted,SubscriptionExpires,AutoRenew")] Tenant tenant)
    {
        if (id != tenant.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(tenant);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TenantExists(tenant.Id))
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
        return View(tenant);
    }

    // GET: TENANTS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var tenant = await _context.Tenant
            .FirstOrDefaultAsync(m => m.Id == id);
        if (tenant == null)
        {
            return NotFound();
        }

        return View(tenant);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        if (id == null)
            return NotFound();

        var tenant = await _context.Tenant
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (tenant == null)
            return NotFound();

        await _tenantDeletionService.DeleteTenantAsync(tenant.Id);

        return RedirectToAction(nameof(Index));
    }

    private bool TenantExists(int? id)
    {
        return _context.Tenant.Any(e => e.Id == id);
    }
}
