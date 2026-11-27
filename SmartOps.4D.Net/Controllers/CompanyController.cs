
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Services;

public class CompanyController : Controller
{
    private readonly AppDBContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly ITenantDeletionService _tenantDeletionService;
    private readonly SignInManager<AppUser> _signInManager;

    public CompanyController(
        AppDBContext context,
        ITenantContext tenantContext,
        ITenantDeletionService tenantDeletionService,
        SignInManager<AppUser> signInManager)
    {
        _context = context;
        _tenantContext = tenantContext;
        _tenantDeletionService = tenantDeletionService;
        _signInManager = signInManager;
    }

    // GET: Company/DeleteAccount
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAccount()
    {
        if (_tenantContext.TenantId is not int tenantId)
            return NotFound();

        var tenant = await _context.Tenant
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            return NotFound();

        return View(tenant);
    }

    // POST: Company/DeleteAccount
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAccount(
        string confirmationCode)
    {
        if (_tenantContext.TenantId is not int tenantId)
            return NotFound();

        var tenant = await _context.Tenant
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(confirmationCode) ||
            !string.Equals(
                confirmationCode.Trim(),
                tenant.Code,
                StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "",
                "The company code does not match.");

            return View(tenant);
        }

        await _tenantDeletionService.DeleteTenantAsync(tenantId);

        await _signInManager.SignOutAsync();

        return RedirectToAction("Index", "Home");
    }

    // GET: TENANTS
    public async Task<IActionResult> Index()    
    {

        return View(await _context.Tenant.ToListAsync());
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
    public async Task<IActionResult> Create([Bind("Id,Name,Code,IsActive,Created,Address1,Address2,Town,County,Postcode,Telephone,Email,Website,Logo,LogoUpdated,SubscriptionPlan,SubscriptionStarted,SubscriptionExpires,AutoRenew")] Tenant tenant)
    {
        if (ModelState.IsValid)
        {
            _context.Add(tenant);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(tenant);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit()
    {
        if (_tenantContext.TenantId == null)
            return NotFound();

        var tenant = await _context.Tenant
            .FirstOrDefaultAsync(t =>
                t.Id == _tenantContext.TenantId);

        if (tenant == null)
            return NotFound();

        return View(tenant);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(
        Tenant model,
        IFormFile? LogoFile)
    {
        if (_tenantContext.TenantId == null)
            return NotFound();

        var tenant = await _context.Tenant
            .FirstOrDefaultAsync(t =>
                t.Id == _tenantContext.TenantId);

        if (tenant == null)
            return NotFound();

        if (!ModelState.IsValid)
            return View(model);

        tenant.Name = model.Name;
        tenant.Address1 = model.Address1;
        tenant.Address2 = model.Address2;
        tenant.Town = model.Town;
        tenant.County = model.County;
        tenant.Postcode = model.Postcode;
        tenant.Telephone = model.Telephone;
        tenant.Email = model.Email;
        tenant.Website = model.Website;

        tenant.Theme = SiteThemes.All.ContainsKey(model.Theme)
            ? model.Theme
            : "Midnight";

        if (LogoFile != null &&
            LogoFile.Length > 0)
        {
            using var ms = new MemoryStream();

            await LogoFile.CopyToAsync(ms);

            tenant.Logo = ms.ToArray();
            tenant.LogoUpdated = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        TempData["Success"] =
            "Company details updated successfully.";

        return RedirectToAction(nameof(Edit));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Plans()
    {
        if (_tenantContext.TenantId == null)
            return NotFound();

        var tenant = await _context.Tenant
            .FirstOrDefaultAsync(t =>
                t.Id == _tenantContext.TenantId);

        if (tenant == null)
            return NotFound();

        return View(tenant);
    }


    [HttpGet]
    public async Task<IActionResult> Logo()
    {
        if (_tenantContext.TenantId == null)
            return NotFound();

        var tenant = await _context.Tenant
            .FirstOrDefaultAsync(t =>
                t.Id == _tenantContext.TenantId);

        if (tenant?.Logo == null)
            return NotFound();

        return File(tenant.Logo, "image/png");
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

    // POST: TENANTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var tenant = await _context.Tenant.FindAsync(id);
        if (tenant != null)
        {
            _context.Tenant.Remove(tenant);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool TenantExists(int? id)
    {
        return _context.Tenant.Any(e => e.Id == id);
    }
}
