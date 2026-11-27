using SmartOps.Web.Helpers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Models;
using SmartOps.Web.Data;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class SpotChecksController : Controller
{
    private readonly AppDBContext _context;

    public SpotChecksController(AppDBContext context)
    {
        _context = context;
    }

    // GET: SPOTCHECKS
    public async Task<IActionResult> Index(int page = 1, string? branchCode = null)
    {
        if (_context.SpotChecks == null)
        {
            return Problem("Entity set 'PropertyRepairsContext.SpotChecks' is null.");
        }

        branchCode = UserBranchDefaults.Filter(_context, HttpContext, branchCode);
        var query = _context.SpotChecks.AsNoTracking();
        await BranchIndexFilters.PopulateAsync(_context, ViewData, branchCode, query.Select(x => x.branch));
        if (branchCode != null) query = query.Where(x => x.branch.Trim() == branchCode);

        var items = await query
            .OrderByDescending(s => s.DateTimeCompleted).ThenByDescending(s => s.Id)
            .ToRecordPageAsync(page, ViewData, pageSize: 20);

        return View(items);
    }

    // GET: SPOTCHECKS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var spotcheck = await _context.SpotChecks
            .FirstOrDefaultAsync(m => m.Id == id);
        if (spotcheck == null)
        {
            return NotFound();
        }

        string pattern = $"{spotcheck.ContractCode:00000000}_sSTChk__.jpg"; // using _ as a wildcard ( would have been cAZ and dAZ )
        string signaturePattern = $"{spotcheck.ContractCode:00000000}_000SCSig.jpg";

        var photoimages = _context.Images
            .Where(x => EF.Functions.Like(x.Filename, pattern) || EF.Functions.Like(x.Filename, signaturePattern))
            .ToList();

        ViewBag.Images = photoimages ?? new List<PhotoImage>();

        //ViewBag.images = new List<PhotoImage>();

        return View(spotcheck);
    }

    // GET: SPOTCHECKS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: SPOTCHECKS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Add1,Add2,Add3,CheckID,ContractCode,DamageDesc,DateTimeCompleted,DiaryDate,Guid,Id,Instructions,Name,Phone1,Phone2,Phone3,Postcode,SpotCheckAnswersJson,SpotCheckQuestionSetVersion,SpotCheckQuestionsSnapshotJson,Time,bComplete,bCorrectContract,bSent,branch,date_done,no_of_photos,uspot_signed,uspot_signeddate")] SpotCheck spotcheck)
    {
        if (ModelState.IsValid)
        {
            _context.Add(spotcheck);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(spotcheck);
    }

    // GET: SPOTCHECKS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var spotcheck = await _context.SpotChecks.FindAsync(id);
        if (spotcheck == null)
        {
            return NotFound();
        }
        return View(spotcheck);
    }

    // POST: SPOTCHECKS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Add1,Add2,Add3,CheckID,ContractCode,DamageDesc,DateTimeCompleted,DiaryDate,Guid,Id,Instructions,Name,Phone1,Phone2,Phone3,Postcode,SpotCheckAnswersJson,SpotCheckQuestionSetVersion,SpotCheckQuestionsSnapshotJson,Time,bComplete,bCorrectContract,bSent,branch,date_done,no_of_photos,uspot_signed,uspot_signeddate")] SpotCheck spotcheck)
    {
        if (id != spotcheck.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(spotcheck);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SpotCheckExists(spotcheck.Id))
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
        return View(spotcheck);
    }

    // GET: SPOTCHECKS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var spotcheck = await _context.SpotChecks
            .FirstOrDefaultAsync(m => m.Id == id);
        if (spotcheck == null)
        {
            return NotFound();
        }

        return View(spotcheck);
    }

    // POST: SPOTCHECKS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var spotcheck = await _context.SpotChecks.FindAsync(id);
        if (spotcheck != null)
        {
            _context.SpotChecks.Remove(spotcheck);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool SpotCheckExists(int? id)
    {
        return _context.SpotChecks.Any(e => e.Id == id);
    }
}
