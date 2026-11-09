using SmartOps.Web.Helpers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Models;
using SmartOps.Web.Data;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class ToolChecksController : Controller
{
    private readonly AppDBContext _context;

    public ToolChecksController(AppDBContext context)
    {
        _context = context;
    }

    // GET: TOOLCHECKS
    public async Task<IActionResult> Index(int page = 1, string? branchCode = null)
    {
        if (_context.ToolChecks == null)
        {
            return Problem("Entity set 'PropertyRepairsContext.ToolChecks' is null.");
        }

        branchCode = UserBranchDefaults.Filter(_context, HttpContext, branchCode);
        var query = _context.ToolChecks.AsNoTracking();
        await BranchIndexFilters.PopulateAsync(_context, ViewData, branchCode, query.Select(x => x.branch));
        if (branchCode != null) query = query.Where(x => x.branch.Trim() == branchCode);

        var items = await query
            .OrderByDescending(t => t.DateTimeCompleted).ThenByDescending(t => t.Id)
            .ToRecordPageAsync(page, ViewData, pageSize: 20);

        return View(items);
    }

    // GET: TOOLCHECKS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var toolcheck = await _context.ToolChecks
            .FirstOrDefaultAsync(m => m.Id == id);
        if (toolcheck == null)
        {
            return NotFound();
        }

        ViewData["AuditSign"] = _context.Images.Where(x => x.Filename == toolcheck.signature_filename).FirstOrDefault()?.Filename;
        ViewData["FitSign"] = _context.Images.Where(x => x.Filename == toolcheck.signature_filename2).FirstOrDefault()?.Filename;

        string pattern = $"{toolcheck.CheckID}_Tools___.jpg"; // using _ as a wildcard ( would have been cAZ and dAZ )

        var photoimages = _context.Images
            .Where(x => EF.Functions.Like(x.Filename, pattern))
            .ToList();

        ViewBag.Images = photoimages ?? new List<PhotoImage>();

        return View(toolcheck);
    }

    // GET: TOOLCHECKS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: TOOLCHECKS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Guid,files_a,pliers_a,chisels_a,pincers_a,scraper_a,hacksaw_a,crowbar_a,handsaw_a,molegrips_a,sidecutters_a,hammer_a,spiritlevel_a,screwdrivers_a,bolsterchisel_a,setsquare_a,stanleyknife_a,clubhammer_a,tapemeasure_a,slidingbevel_a,glazingshovel_a,pointingtrowel_a,setofallenkeys_a,adjustablespanner_a,augerbits_a,nailpunch_a,puttyknife_a,socketset_a,copingsaw_a,augerbitsjoin_a,nailpunchjoin_a,puttyknifejoin_a,socketsetjoin_a,copingsawjoin_a,rivetgunjoin_a,files_f,pliers_f,chisels_f,pincers_f,scraper_f,hacksaw_f,crowbar_f,handsaw_f,molegrips_f,sidecutters_f,hammer_f,spiritlevel_f,screwdrivers_f,bolsterchisel_f,setsquare_f,stanleyknife_f,clubhammer_f,tapemeasure_f,slidingbevel_f,glazingshovel_f,pointingtrowel_f,setofallenkeys_f,adjustablespanner_f,augerbits_f,nailpunch_f,puttyknife_f,socketset_f,copingsaw_f,augerbitsjoin_f,nailpunchjoin_f,puttyknifejoin_f,socketsetjoin_f,copingsawjoin_f,rivetgunjoin_f,date_done,bComplete,bSent,bSigned,bSigned2,signature_filename,signature_filename2,signature_printed,signature_printed2,registration,branch,CheckID,photo_filename")] ToolCheck toolcheck)
    {
        if (ModelState.IsValid)
        {
            _context.Add(toolcheck);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(toolcheck);
    }

    // GET: TOOLCHECKS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var toolcheck = await _context.ToolChecks.FindAsync(id);
        if (toolcheck == null)
        {
            return NotFound();
        }
        return View(toolcheck);
    }

    // POST: TOOLCHECKS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Guid,files_a,pliers_a,chisels_a,pincers_a,scraper_a,hacksaw_a,crowbar_a,handsaw_a,molegrips_a,sidecutters_a,hammer_a,spiritlevel_a,screwdrivers_a,bolsterchisel_a,setsquare_a,stanleyknife_a,clubhammer_a,tapemeasure_a,slidingbevel_a,glazingshovel_a,pointingtrowel_a,setofallenkeys_a,adjustablespanner_a,augerbits_a,nailpunch_a,puttyknife_a,socketset_a,copingsaw_a,augerbitsjoin_a,nailpunchjoin_a,puttyknifejoin_a,socketsetjoin_a,copingsawjoin_a,rivetgunjoin_a,files_f,pliers_f,chisels_f,pincers_f,scraper_f,hacksaw_f,crowbar_f,handsaw_f,molegrips_f,sidecutters_f,hammer_f,spiritlevel_f,screwdrivers_f,bolsterchisel_f,setsquare_f,stanleyknife_f,clubhammer_f,tapemeasure_f,slidingbevel_f,glazingshovel_f,pointingtrowel_f,setofallenkeys_f,adjustablespanner_f,augerbits_f,nailpunch_f,puttyknife_f,socketset_f,copingsaw_f,augerbitsjoin_f,nailpunchjoin_f,puttyknifejoin_f,socketsetjoin_f,copingsawjoin_f,rivetgunjoin_f,date_done,bComplete,bSent,bSigned,bSigned2,signature_filename,signature_filename2,signature_printed,signature_printed2,registration,branch,CheckID,photo_filename")] ToolCheck toolcheck)
    {
        if (id != toolcheck.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(toolcheck);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ToolCheckExists(toolcheck.Id))
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
        return View(toolcheck);
    }

    // GET: TOOLCHECKS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var toolcheck = await _context.ToolChecks
            .FirstOrDefaultAsync(m => m.Id == id);
        if (toolcheck == null)
        {
            return NotFound();
        }

        return View(toolcheck);
    }

    // POST: TOOLCHECKS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var toolcheck = await _context.ToolChecks.FindAsync(id);
        if (toolcheck != null)
        {
            _context.ToolChecks.Remove(toolcheck);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool ToolCheckExists(int? id)
    {
        return _context.ToolChecks.Any(e => e.Id == id);
    }
}
