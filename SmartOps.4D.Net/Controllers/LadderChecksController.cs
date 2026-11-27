using SmartOps.Web.Helpers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Models;
using SmartOps.Web.Data;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class LadderChecksController : Controller
{
    private readonly AppDBContext _context;

    public LadderChecksController(AppDBContext context)
    {
        _context = context;
    }

    // GET: LADDERCHECKS
    public async Task<IActionResult> Index(int page = 1, string? branchCode = null)
    {
        if (_context.LadderChecks == null)
        {
            return Problem("Entity set 'PropertyRepairsContext.LadderChecks' is null.");
        }

        branchCode = UserBranchDefaults.Filter(_context, HttpContext, branchCode);
        var query = _context.LadderChecks.AsNoTracking();
        await BranchIndexFilters.PopulateAsync(_context, ViewData, branchCode, query.Select(x => x.branch));
        if (branchCode != null) query = query.Where(x => x.branch.Trim() == branchCode);

        var items = await query
            .OrderByDescending(l => l.DateTimeCompleted).ThenByDescending(l => l.Id)
            .ToRecordPageAsync(page, ViewData, pageSize: 20);

        return View(items);
    }

    // GET: LADDERCHECKS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var laddercheck = await _context.LadderChecks
            .FirstOrDefaultAsync(m => m.Id == id);
        if (laddercheck == null)
        {
            return NotFound();
        }

        ViewData["Sign1"] = _context.Images.Where(x => x.Filename == laddercheck.signature_filename).FirstOrDefault()?.Filename;
        ViewData["Sign2"] = _context.Images.Where(x => x.Filename == laddercheck.signature_filename_2).FirstOrDefault()?.Filename;

        string pattern = $"{laddercheck.CheckID}_LadPi___.jpg"; // using _ as a wildcard ( would have been cAZ and dAZ )

        var photoimages = _context.Images
            .Where(x => EF.Functions.Like(x.Filename, pattern))
            .ToList();

        ViewBag.Images = photoimages ?? new List<PhotoImage>();

        return View(laddercheck);
    }

    // GET: LADDERCHECKS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: LADDERCHECKS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Guid,date_done,branch,ladder_number,ladder_type,registration,fitter_surveyor_name,managers_name,CheckID,in_reasonable_condition,rungs_missing_or_loose,stiles_damaged_or_bent,any_cracks,any_corrosion,rubber_plastic_feet,sharp_or_metal_splinters,rungs_dented,painted_or_decorated,hooks_sit_properly,ladders_been_repaired,any_damage,comments,no_of_photos,bSent,bComplete,bSigned,bSigned2,signature_filename,signature_filename_2")] LadderCheck laddercheck)
    {
        if (ModelState.IsValid)
        {
            _context.Add(laddercheck);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(laddercheck);
    }

    // GET: LADDERCHECKS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var laddercheck = await _context.LadderChecks.FindAsync(id);
        if (laddercheck == null)
        {
            return NotFound();
        }
        return View(laddercheck);
    }

    // POST: LADDERCHECKS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Guid,date_done,branch,ladder_number,ladder_type,registration,fitter_surveyor_name,managers_name,CheckID,in_reasonable_condition,rungs_missing_or_loose,stiles_damaged_or_bent,any_cracks,any_corrosion,rubber_plastic_feet,sharp_or_metal_splinters,rungs_dented,painted_or_decorated,hooks_sit_properly,ladders_been_repaired,any_damage,comments,no_of_photos,bSent,bComplete,bSigned,bSigned2,signature_filename,signature_filename_2")] LadderCheck laddercheck)
    {
        if (id != laddercheck.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(laddercheck);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LadderCheckExists(laddercheck.Id))
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
        return View(laddercheck);
    }

    // GET: LADDERCHECKS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var laddercheck = await _context.LadderChecks
            .FirstOrDefaultAsync(m => m.Id == id);
        if (laddercheck == null)
        {
            return NotFound();
        }

        return View(laddercheck);
    }

    // POST: LADDERCHECKS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var laddercheck = await _context.LadderChecks.FindAsync(id);
        if (laddercheck != null)
        {
            _context.LadderChecks.Remove(laddercheck);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool LadderCheckExists(int? id)
    {
        return _context.LadderChecks.Any(e => e.Id == id);
    }
}
