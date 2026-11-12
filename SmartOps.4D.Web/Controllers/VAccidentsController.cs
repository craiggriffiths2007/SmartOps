using SmartOps.Web.Helpers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Models;
using SmartOps.Web.Data;

[Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class VAccidentsController : Controller
{
    private readonly AppDBContext _context;

    public VAccidentsController(AppDBContext context)
    {
        _context = context;
    }

    // GET: VACCIDENTS
    public async Task<IActionResult> Index(int page = 1)
    {
        if (_context.VAccidents == null)
        {
            return Problem("Entity set 'PropertyRepairsContext.VAccidents' is null.");
        }

        var items = await _context.VAccidents
            .OrderByDescending(v => v.DateTimeCompleted).ThenByDescending(v => v.Id)
            .ToRecordPageAsync(page, ViewData, pageSize: 20);

        return View(items);
    }

    // GET: VACCIDENTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var vaccident = await _context.VAccidents
            .FirstOrDefaultAsync(m => m.Id == id);
        if (vaccident == null)
        {
            return NotFound();
        }

        ViewData["Sign"] = _context.Images.Where(x => x.Filename == vaccident.CheckID+"_sig.jpg").FirstOrDefault()?.Filename;

        string drawingName = vaccident.CheckID + "_Drawing_.jpg";
        ViewData["Drawing"] = await _context.Images.Where(x => x.Filename == drawingName)
            .Select(x => x.Filename).FirstOrDefaultAsync();
        string photoPrefix = vaccident.CheckID + "_photo";
        ViewBag.Images = await _context.Images
            .Where(x => x.Filename.StartsWith(photoPrefix) && x.Filename.EndsWith(".jpg"))
            .OrderBy(x => x.Filename).ToListAsync();

        return View(vaccident);
    }

    // GET: VACCIDENTS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: VACCIDENTS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Guid,date_time,bSent,bComplete,no_of_photos,y_signed,c_drawings,AccidentAnswersJson,AccidentQuestionsSnapshotJson,AccidentQuestionSetVersion")] VAccident vaccident)
    {
        if (ModelState.IsValid)
        {
            _context.Add(vaccident);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(vaccident);
    }

    // GET: VACCIDENTS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var vaccident = await _context.VAccidents.FindAsync(id);
        if (vaccident == null)
        {
            return NotFound();
        }
        return View(vaccident);
    }

    // POST: VACCIDENTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Guid,date_time,bSent,bComplete,no_of_photos,y_signed,c_drawings,AccidentAnswersJson,AccidentQuestionsSnapshotJson,AccidentQuestionSetVersion")] VAccident vaccident)
    {
        if (id != vaccident.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(vaccident);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VAccidentExists(vaccident.Id))
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
        return View(vaccident);
    }

    // GET: VACCIDENTS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var vaccident = await _context.VAccidents
            .FirstOrDefaultAsync(m => m.Id == id);
        if (vaccident == null)
        {
            return NotFound();
        }

        return View(vaccident);
    }

    // POST: VACCIDENTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var vaccident = await _context.VAccidents.FindAsync(id);
        if (vaccident != null)
        {
            _context.VAccidents.Remove(vaccident);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool VAccidentExists(int? id)
    {
        return _context.VAccidents.Any(e => e.Id == id);
    }
}
