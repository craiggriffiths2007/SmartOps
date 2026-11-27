using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class FAccidentsController : Controller
    {
        private readonly AppDBContext _context;

        public FAccidentsController(AppDBContext context)
        {
            _context = context;
        }

        // GET: FAccidents
        public async Task<IActionResult> Index(int page = 1)
        {
            if (_context.FAccidents == null)
            {
                return Problem("Entity set 'PropertyRepairsContext.FAccidents'  is null.");
            }

            var items = await _context.FAccidents
                .OrderByDescending(f => f.DateTimeCompleted).ThenByDescending(f => f.Id)
                .ToRecordPageAsync(page, ViewData, pageSize: 20);

            return View(items);
        }

        // GET: FAccidents/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fAccidents = await _context.FAccidents
                .FirstOrDefaultAsync(m => m.Id == id);
            if (fAccidents == null)
            {
                return NotFound();
            }

            ViewData["sig_fname1"] = _context.Images.Where(x => x.Filename == fAccidents.sig_fname1).FirstOrDefault()?.Filename;
            ViewData["sig_fname2"] = _context.Images.Where(x => x.Filename == fAccidents.sig_fname2).FirstOrDefault()?.Filename;

            string prefix = fAccidents.CheckID + "_FAcci";
            ViewBag.Images = await _context.Images.Where(x => x.Filename.StartsWith(prefix) && x.Filename.EndsWith(".jpg"))
                .OrderBy(x => x.Filename).ToListAsync();

            return View(fAccidents);
        }

        // GET: FAccidents/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: FAccidents/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Guid,CheckID,date_time,bSent,bComplete,Type,sig_fname1,sig_fname2,no_of_photos,AccidentAnswersJson,AccidentQuestionsSnapshotJson,AccidentQuestionSetVersion")] FAccident fAccidents)
        {
            if (ModelState.IsValid)
            {
                _context.Add(fAccidents);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(fAccidents);
        }

        // GET: FAccidents/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fAccidents = await _context.FAccidents.FindAsync(id);
            if (fAccidents == null)
            {
                return NotFound();
            }
            return View(fAccidents);
        }

        // POST: FAccidents/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Guid,CheckID,date_time,bSent,bComplete,Type,sig_fname1,sig_fname2,no_of_photos,AccidentAnswersJson,AccidentQuestionsSnapshotJson,AccidentQuestionSetVersion")] FAccident fAccidents)
        {
            if (id != fAccidents.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(fAccidents);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FAccidentsExists(fAccidents.Id))
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
            return View(fAccidents);
        }

        // GET: FAccidents/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var fAccidents = await _context.FAccidents
                .FirstOrDefaultAsync(m => m.Id == id);
            if (fAccidents == null)
            {
                return NotFound();
            }

            return View(fAccidents);
        }

        // POST: FAccidents/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var fAccidents = await _context.FAccidents.FindAsync(id);
            if (fAccidents != null)
            {
                _context.FAccidents.Remove(fAccidents);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FAccidentsExists(int id)
        {
            return _context.FAccidents.Any(e => e.Id == id);
        }
    }
}
