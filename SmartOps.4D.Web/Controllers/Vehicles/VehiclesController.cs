using SmartOps.Web.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class VehiclesController : Controller
    {
        private readonly AppDBContext _context;

        public VehiclesController(AppDBContext context)
        {
            _context = context;
        }

        // GET: Vehicles
        public async Task<IActionResult> Index(int page = 1, string? branchCode = null)
        {
            if (_context.Vehicles == null)
            {
                return Problem("Entity set 'PropertyRepairsContext.Vehicles' is null.");
            }

            branchCode = UserBranchDefaults.Filter(_context, HttpContext, branchCode);
            var query = _context.Vehicles.AsNoTracking().Include(v => v.Branch).AsQueryable();
            await BranchIndexFilters.PopulateAsync(_context, ViewData, branchCode,
                query.Select(v => v.Branch == null ? null : v.Branch.Code));
            if (branchCode != null)
                query = query.Where(v => v.Branch != null && v.Branch.Code != null && v.Branch.Code.Trim() == branchCode);
            // Vehicles do not store a date; new records are shown first by ID.
            var appDBContext = query.OrderByDescending(v => v.Id);

            return View(await appDBContext.ToRecordPageAsync(page, ViewData, pageSize: 20));
        }

        // GET: Vehicles/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
                .Include(v => v.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicle == null)
            {
                return NotFound();
            }

            // Check for vehicle checks

            return View(vehicle);
        }

        // GET: Vehicles/Create
        public IActionResult Create()
        {
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", UserBranchDefaults.Id(_context, User));
            return View();
        }

        // POST: Vehicles/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,BranchId,Name,Registration,Description,Type")] Vehicle vehicle)
        {
            if (!UserBranchDefaults.AllowsSelection(_context, User, vehicle.BranchId == 0 ? null : vehicle.BranchId, UserBranchDefaults.Id(_context, User)))
                return BadRequest("Your branch selection is locked.");
            if (ModelState.IsValid)
            {
                _context.Add(vehicle);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id = vehicle.Id });
            }
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", vehicle.BranchId);
            return View(vehicle);
        }

        // GET: Vehicles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle == null)
            {
                return NotFound();
            }
            ViewData["BranchId"] = new SelectList(_context.Branches, "Id", "Name", vehicle.BranchId);
            return View(vehicle);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,BranchId,Name,Registration,Description,Type")]
            Vehicle vehicle)
        {
            if (id != vehicle.Id)
                return NotFound();
            var originalBranch = await _context.Vehicles.Where(v => v.Id == id).Select(v => (int?)v.BranchId).FirstOrDefaultAsync();
            if (!UserBranchDefaults.AllowsSelection(_context, User, vehicle.BranchId, originalBranch))
                return BadRequest("Your branch selection is locked.");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingVehicle = await _context.Vehicles
                        .FirstOrDefaultAsync(x => x.Id == id);

                    if (existingVehicle == null)
                        return NotFound();

                    existingVehicle.BranchId = vehicle.BranchId;
                    existingVehicle.Name = vehicle.Name;
                    existingVehicle.Registration = vehicle.Registration;
                    existingVehicle.Description = vehicle.Description;
                    existingVehicle.Type = vehicle.Type;

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VehicleExists(vehicle.Id))
                        return NotFound();

                    throw;
                }

                return RedirectToAction(nameof(Details), new { id = vehicle.Id });
            }

            ViewData["BranchId"] = new SelectList(
                _context.Branches,
                "Id",
                "Name",
                vehicle.BranchId);

            return View(vehicle);
        }

        // GET: Vehicles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicle = await _context.Vehicles
                .Include(v => v.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicle == null)
            {
                return NotFound();
            }

            return View(vehicle);
        }

        // POST: Vehicles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var vehicle = await _context.Vehicles.FindAsync(id);
            if (vehicle != null)
            {
                _context.Vehicles.Remove(vehicle);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VehicleExists(int id)
        {
            return _context.Vehicles.Any(e => e.Id == id);
        }
    }
}
