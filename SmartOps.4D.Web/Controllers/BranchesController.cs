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
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.Admin)]
    public class BranchesController : Controller
    {
        private readonly AppDBContext _context;

        public BranchesController(AppDBContext context)
        {
            _context = context;
        }

        // GET: Branches
        public async Task<IActionResult> Index(int page = 1)
        {
            if (_context.Branches == null)
            {
                return Problem("Entity set 'PropertyRepairsContext.Branches' is null.");
            }

            var items = await _context.Branches
                .OrderByDescending(b => b.Id)
                .ToRecordPageAsync(page, ViewData, pageSize: 20);

            return View(items);
        }

        // GET: Branches/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .FirstOrDefaultAsync(m => m.Id == id);
            if (branch == null)
            {
                return NotFound();
            }

            return View(branch);
        }

        // GET: Branches/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Branches/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Code,Description")] Branch branch)
        {
            if (ModelState.IsValid)
            {
                _context.Add(branch);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id = branch.Id });
            }
            return View(branch);
        }

        // GET: Branches/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
            {
                return NotFound();
            }
            return View(branch);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
                    int id,
                    [Bind("Id,Name,Code,Description")] Branch branch)
        {
            if (id != branch.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBranch = await _context.Branches
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (existingBranch == null)
                        return NotFound();

                    existingBranch.Name = branch.Name;
                    existingBranch.Code = branch.Code;
                    existingBranch.Description = branch.Description;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BranchExists(branch.Id))
                        return NotFound();

                    throw;
                }

                return RedirectToAction(nameof(Details), new { id = branch.Id });
            }

            return View(branch);
        }
        // GET: Branches/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .FirstOrDefaultAsync(m => m.Id == id);
            if (branch == null)
            {
                return NotFound();
            }

            return View(branch);
        }

        // POST: Branches/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch != null)
            {
                _context.Branches.Remove(branch);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id);
        }
    }
}
