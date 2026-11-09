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
using SmartOps.Web.ViewModels;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
    public class JobHeadersController : Controller
    {
        private readonly Data.AppDBContext _context;
        private readonly ITenantContext _tenantContext;

        public JobHeadersController(
            AppDBContext context,
            ITenantContext tenantContext)
        {
            _context = context;
            _tenantContext = tenantContext;
        }

        // GET: Headers
        public async Task<IActionResult> Index(int page = 1)
        {
              return _context.JobHeader != null ?
                          View(await _context.JobHeader.OrderByDescending(x => x.DateTimeCompleted).ThenByDescending(x => x.Id).ToRecordPageAsync(page, ViewData)) :
                          Problem("Entity set 'PropertyRepairsContext.Header'  is null.");
        }

        // GET: Headers/Details/5
        public async Task<IActionResult> Details(int? id, bool print = false)
        {
            var viewModel = new JobHeaderItemIndexViewModel();

            if (id == null || _context.JobHeader == null)
            {
                return NotFound();
            }

            viewModel.JobHeader = await _context.JobHeader
                .Where(m => m.Id == id)
                .Include(m => m.GarageDoorMotor)
                .FirstOrDefaultAsync();
            if (viewModel.JobHeader == null)
            {
                return NotFound();
            }
            else
            {
                var items = await _context.CustomItems.Where(x => x.HeaderId == viewModel.JobHeader.Id && x.ParentItemGuid == null)
                    .Select(x => x).ToListAsync();
                viewModel.SurveyItems = items.OrderBy(i => i.item_number).Select(i => i.AsSurveyItem());

                if (print && viewModel.JobHeader.JobType is enum_job_type.Survey or enum_job_type.Fitting)
                {
                    string prefix = $"{viewModel.JobHeader.ContractCode:00000000}_";
                    bool fitting = viewModel.JobHeader.JobType == enum_job_type.Fitting;
                    viewModel.Images = await _context.Images
                        .Where(x => x.Filename.StartsWith(prefix + "cAZ") ||
                                    x.Filename.StartsWith(prefix + "d") ||
                                    x.Filename.StartsWith(prefix + "col") ||
                                    x.Filename.StartsWith(prefix + "sCU") ||
                                    (fitting && x.Filename.StartsWith(prefix + "f")))
                        .ToListAsync();
                    return View("PrintReport", viewModel);
                }

                string photoPattern = $"{viewModel.JobHeader.ContractCode:00000000}_cAZHou%";
                string videoPattern = $"{viewModel.JobHeader.ContractCode:00000000}_Videos%";
                switch (viewModel.JobHeader.JobType)
                {
                    case enum_job_type.Survey:
                        photoPattern = $"{viewModel.JobHeader.ContractCode:00000000}_cAZHou%";
                        break;
                    case enum_job_type.Fitting:
                        photoPattern = $"{viewModel.JobHeader.ContractCode:00000000}_f%";
                        videoPattern = $"{viewModel.JobHeader.ContractCode:00000000}_Videof%";
                        break;
                    case enum_job_type.Remedial:
                        photoPattern = $"{viewModel.JobHeader.ContractCode:00000000}_r%";
                        videoPattern = $"{viewModel.JobHeader.ContractCode:00000000}_Videor%";
                        break;

                }

                viewModel.Images = _context.Images
                    .Where(x => EF.Functions.Like(x.Filename, photoPattern) || EF.Functions.Like(x.Filename, videoPattern))
                    .ToList();

            }

            var parentJob = await _context.Job
                .FirstOrDefaultAsync(j => j.ContractCode == viewModel.JobHeader.ContractCode);
            if (parentJob != null)
            {
                ViewBag.ParentJobId = parentJob.Id;
            }

            return View(viewModel);
        }

        public async Task<ActionResult> GarageMotor(int? id)
        {
            if (id == null)
                return NotFound();

            var jobHeader = await _context.JobHeader
                .Include(m => m.GarageDoorMotor)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jobHeader == null)
                return NotFound();

            if (_tenantContext.TenantId == null)
                return NotFound();

            var tenant = await _context.Tenant
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t =>
                    t.Id == _tenantContext.TenantId.Value);

            if (tenant == null)
                return NotFound();

            string fname = string.Format(
                "{0}_fmotorcustomer.jpg",
                jobHeader.ContractCode);

            var signature = await _context.Images
                .Where(x => x.Filename == fname)
                .Select(x => x.Filename)
                .FirstOrDefaultAsync();

            var fitterSignature = await _context.Images
                .Where(x => x.Filename == jobHeader.ContractCode + "_fmotor.jpg")
                .Select(x => x.Filename).FirstOrDefaultAsync();

            var model = new GarageDoorMotorViewModel
            {
                JobHeader = jobHeader,
                FitterSignatureFilename = fitterSignature ?? "",

                SignatureFilename =
                    signature ?? "",

                CompanyName =
                    tenant.Name,

                CompanyAddress1 =
                    tenant.Address1 ?? "",

                CompanyAddress2 =
                    tenant.Address2 ?? "",

                CompanyAddress3 =
                    BuildAddressLine3(
                        tenant.Town,
                        tenant.County),

                CompanyPostcode =
                    tenant.Postcode ?? "",

                CompanyTelephone =
                    tenant.Telephone ?? "",

                CompanyEmail =
                    tenant.Email ?? "",

                LogoUrl =
                    tenant.Logo != null && tenant.Logo.Length > 0
                        ? Url.Action(
                            "Logo",
                            "Company",
                            new
                            {
                                v = tenant.LogoUpdated?.Ticks ?? 0
                            }) ?? ""
                        : ""
            };

            return View(model);
            /*
            var job = await _context.JobHeader.Where(m => m.Id == id)
                                        .Include(j => j.GarageDoorMotor)
                                        .FirstOrDefaultAsync();

            return View(job);*/
        }

        public async Task<ActionResult> SignedMandate(int? id)
        {
            if (id == null)
                return NotFound();

            var jobHeader = await _context.JobHeader
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jobHeader == null)
                return NotFound();

            if (_tenantContext.TenantId == null)
                return NotFound();

            var tenant = await _context.Tenant
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t =>
                    t.Id == _tenantContext.TenantId.Value);

            if (tenant == null)
                return NotFound();

            string fname = string.Format(
                "{0:00000000}_fandates.jpg",
                jobHeader.ContractCode);

            var signature = await _context.Images
                .Where(x => x.Filename == fname)
                .Select(x => x.Filename)
                .FirstOrDefaultAsync();

            var model = new SignedMandateViewModel
            {
                JobHeader = jobHeader,

                SignatureFilename =
                    signature ?? "",

                CompanyName =
                    tenant.Name,

                CompanyAddress1 =
                    tenant.Address1 ?? "",

                CompanyAddress2 =
                    tenant.Address2 ?? "",

                CompanyAddress3 =
                    BuildAddressLine3(
                        tenant.Town,
                        tenant.County),

                CompanyPostcode =
                    tenant.Postcode ?? "",

                CompanyTelephone =
                    tenant.Telephone ?? "",

                CompanyEmail =
                    tenant.Email ?? "",

                LogoUrl =
                    tenant.Logo != null && tenant.Logo.Length > 0
                        ? Url.Action(
                            "Logo",
                            "Company",
                            new
                            {
                                v = tenant.LogoUpdated?.Ticks ?? 0
                            }) ?? ""
                        : ""
            };

            return View(model);
        }

        private static string BuildAddressLine3(
                                        string? town,
                                        string? county)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(town))
                parts.Add(town);

            if (!string.IsNullOrWhiteSpace(county))
                parts.Add(county);

            return string.Join(", ", parts);
        }

        // GET: Headers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Headers/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,bSent,ContractCode,si_mpay,readditimage,f_sign_date,bSurvey,current_item_number,survey_complete,directive_complete,garage_door_motor")] JobHeader header)
        {
            if (ModelState.IsValid)
            {
                _context.Add(header);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(header);
        }

        // GET: Headers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.JobHeader == null)
            {
                return NotFound();
            }

            var header = await _context.JobHeader.FindAsync(id);
            if (header == null)
            {
                return NotFound();
            }
            return View(header);
        }
        public async Task<ActionResult> JobInstructions(int? id)
        {
            var jobHeader = await _context.JobHeader.FirstOrDefaultAsync(m => m.Id == id);

            // Return the HTML string directly as a content result with the HTML mime-type
            return Content(jobHeader.JobInstructions, "text/html");
        }
        // POST: Headers/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,bSent,ContractCode,si_mpay,readditimage,f_sign_date,bSurvey,current_item_number,survey_complete,directive_complete,garage_door_motor")] JobHeader header)
        {
            if (id != header.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(header);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!HeaderExists(header.Id))
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
            return View(header);
        }

        // GET: Headers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.JobHeader == null)
            {
                return NotFound();
            }

            var header = await _context.JobHeader
                .FirstOrDefaultAsync(m => m.Id == id);
            if (header == null)
            {
                return NotFound();
            }

            return View(header);
        }

        // POST: Headers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.JobHeader == null)
            {
                return Problem("Entity set 'PropertyRepairsContext.Header'  is null.");
            }
            var header = await _context.JobHeader.FindAsync(id);
            if (header != null)
            {
                _context.JobHeader.Remove(header);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool HeaderExists(int id)
        {
          return (_context.JobHeader?.Any(e => e.Id == id)).GetValueOrDefault();
        }
    }
}
