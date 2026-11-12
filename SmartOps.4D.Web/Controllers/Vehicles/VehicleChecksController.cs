using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageBranch)]
    public class VehicleChecksController : Controller
    {
        private readonly Repo repo;

        public VehicleChecksController(Repo context)
        {
            repo = context;
        }

        // GET: VehicleChecks
        public async Task<IActionResult> Index(int page = 1, string? branchCode = null, string? checkWeek = null)
        {
            if (repo.Db.VehicleCheckHeaders == null)
            {
                return Problem("Entity set 'Repo.Db.VehicleCheckHeaders' is null.");
            }

            var allHeaders = repo.Db.VehicleCheckHeaders.AsNoTracking();
            // Dropdown choices use all tenant data, independently of the active filters/page.
            var availableChecks = await allHeaders
                .Select(x => new { x.BranchCode, x.CheckWeekDate }).Distinct().ToListAsync();
            var branches = await repo.Db.Branches.AsNoTracking()
                .Where(x => x.Code != null && x.Code != "")
                .Select(x => new { x.Code, x.Name }).ToListAsync();
            var branchOptions = branches.Select(x => new SelectListItem
            {
                Value = x.Code!,
                Text = string.IsNullOrWhiteSpace(x.Name) ? "Unnamed branch" : x.Name.Trim()
            }).Concat(availableChecks.Where(x => !string.IsNullOrWhiteSpace(x.BranchCode))
                .Select(x => new SelectListItem { Value = x.BranchCode, Text = "Unknown branch" }))
                .DistinctBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Text, StringComparer.OrdinalIgnoreCase).ToList();
            var weekOptions = availableChecks.Select(x => x.CheckWeekDate)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct()
                .OrderByDescending(x => DateTime.TryParse(x,
                    System.Globalization.CultureInfo.GetCultureInfo("en-GB"),
                    System.Globalization.DateTimeStyles.None, out var date) ? date : DateTime.MinValue)
                .ThenByDescending(x => x, StringComparer.Ordinal)
                .Select(x => new SelectListItem { Value = x, Text = x }).ToList();

            branchCode = UserBranchDefaults.Filter(repo.Db, HttpContext, branchCode);
            if (UserBranchDefaults.IsLocked(repo.Db, User)) ModelState.Remove(nameof(branchCode));
            checkWeek = string.IsNullOrWhiteSpace(checkWeek) ? null : checkWeek;
            var items = allHeaders;
            if (branchCode != null) items = items.Where(x => x.BranchCode == branchCode);
            if (checkWeek != null) items = items.Where(x => x.CheckWeekDate == checkWeek);
            var headers = await items.OrderByDescending(v => v.DateTimeCompleted).ThenByDescending(v => v.Id)
                .ToRecordPageAsync(page, ViewData, pageSize: 20);
            var headerIds = headers.Select(x => x.Id).ToList();
            // Load registrations for this page in one tenant-scoped query.
            var vehicles = await repo.Db.VehicleChecks
                .Where(x => headerIds.Contains(x.VehicleCheckHeaderId))
                .Select(x => new { x.VehicleCheckHeaderId, x.VehicleType, x.Registration, x.is_complete })
                .ToListAsync();
            var vehiclesByHeader = vehicles.ToLookup(x => x.VehicleCheckHeaderId);
            var cards = headers.Select(header => new VehicleCheckCardViewModel
            {
                Header = header,
                Registrations = vehiclesByHeader[header.Id]
                    .GroupBy(x => (enum_vehicle_type)x.VehicleType)
                    .ToDictionary(group => group.Key, group => group
                        .GroupBy(x => string.IsNullOrWhiteSpace(x.Registration)
                            ? "Registration not recorded" : x.Registration.Trim(), StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(registration => new VehicleCheckRegistrationViewModel
                        {
                            Registration = registration.Key,
                            IsIncomplete = registration.Any(x => x.is_complete == false)
                        }).ToList())
            }).ToList();
            return View(new VehicleChecksIndexViewModel
            {
                Cards = cards,
                Branches = branchOptions,
                CheckWeeks = weekOptions,
                BranchCode = branchCode,
                CheckWeek = checkWeek
            });
        }

        // GET: VehicleChecks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            var viewModel = new VehicleCheckHeaderIndexViewModel();

            if (id == null)
            {
                return NotFound();
            }

            viewModel.VehicleCheckHeader = await repo.Db.VehicleCheckHeaders
                .FirstOrDefaultAsync(m => m.Id == id);
            if (viewModel.VehicleCheckHeader == null)
            {
                return NotFound();
            }
            else
            {
                var items = await repo.Db.VehicleChecks
                    .Where(x => x.VehicleCheckHeaderId == viewModel.VehicleCheckHeader.Id)
                    .Select(x => new VehicleCheckVehicle(x.Id, (enum_vehicle_type)x.VehicleType, x.Registration)
                    {
                        Destination = x.Destination,
                        Mileage = x.Mileage,
                        DriverName = x.driver_printed,
                        CheckerName = x.checked_printed,
                        IsComplete = x.is_complete,
                        ReasonNotCompleted = x.ReasonNotCompleted
                    })
                    .ToListAsync();
                viewModel.VehicleCheckVehicles = items;

                //List<PhotoImage> photoimages = _context.Images.Where(x => x.Filename.Substring(0, 12) == viewModel.Header.ContractCode + "_cAH").ToList();

                //viewModel.VehicleCheckVehicles = photoimages;
            }
            /*
            var parentVehicleCheck = await _context.VehicleCheckHeaders
                    .FirstOrDefaultAsync(j => j.Id == viewModel.VehicleCheckHeader.ContractCode);
            if (parentJob != null)
            {
                ViewBag.ParentJobId = parentJob.Id;
            }
            */
            return View(viewModel);
        }

        public IActionResult VehicleDetails(int id)
        {
            var viewModel = repo.GetVehicleCheckDetailsViewModel(id);
            if (viewModel.VehicleCheck == null) return NotFound();
            return View("VehicleDetails", viewModel);
        }

        // GET: VehicleChecks/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: VehicleChecks/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,CheckWeekDate,BranchCode,CheckID,bComplete,bSent,TotalDeliveryHGVs,TotalDeliveryVans,TotalFitterVans,TotalSalesCars,CompleteDeliveryHGVs,CompleteDeliveryVans,CompleteFitterVans,CompleteSalesCars")] VehicleCheckHeader vehicleCheckHeader)
        {
            if (ModelState.IsValid)
            {
                repo.Db.Add(vehicleCheckHeader);
                await repo.Db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(vehicleCheckHeader);
        }

        // GET: VehicleChecks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicleCheckHeader = await repo.Db.VehicleCheckHeaders.FindAsync(id);
            if (vehicleCheckHeader == null)
            {
                return NotFound();
            }
            return View(vehicleCheckHeader);
        }

        // POST: VehicleChecks/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,CheckWeekDate,BranchCode,CheckID,bComplete,bSent,TotalDeliveryHGVs,TotalDeliveryVans,TotalFitterVans,TotalSalesCars,CompleteDeliveryHGVs,CompleteDeliveryVans,CompleteFitterVans,CompleteSalesCars")] VehicleCheckHeader vehicleCheckHeader)
        {
            if (id != vehicleCheckHeader.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    repo.Db.Update(vehicleCheckHeader);
                    await repo.Db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VehicleCheckHeaderExists(vehicleCheckHeader.Id))
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
            return View(vehicleCheckHeader);
        }

        // GET: VehicleChecks/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var vehicleCheckHeader = await repo.Db.VehicleCheckHeaders
                .FirstOrDefaultAsync(m => m.Id == id);
            if (vehicleCheckHeader == null)
            {
                return NotFound();
            }

            return View(vehicleCheckHeader);
        }

        // POST: VehicleChecks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var vehicleCheckHeader = await repo.Db.VehicleCheckHeaders.FindAsync(id);
            if (vehicleCheckHeader != null)
            {
                repo.Db.VehicleCheckHeaders.Remove(vehicleCheckHeader);
            }

            await repo.Db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool VehicleCheckHeaderExists(int id)
        {
            return repo.Db.VehicleCheckHeaders.Any(e => e.Id == id);
        }
    }
}
