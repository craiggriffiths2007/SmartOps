using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using SmartOps.Web.Helpers;
using SmartOps.Web.Services;
using System.Data;
using System.Security.Claims;

namespace SmartOps.Web.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
    public class JobsController : Controller
    {
        private readonly Data.AppDBContext _context;
        private readonly SmartOps.Web.Bridge.AccountService _accounts;

        public static string GetLocalIPAddress()
        {
            return "";
        }

        public JobsController(Data.AppDBContext context, SmartOps.Web.Bridge.AccountService accounts)
        {
            _context = context;
            _accounts = accounts;
        }

        // GET: Jobs
        public async Task<IActionResult> Index(int? Id, int? headerId, int? month, int? year, string? view, string? weekStart, string? q, string? allStart, string? allEnd, int? page, int? branchId = null)
        {
            var now = DateTime.Today;
            var savedView = await _accounts.GetDiaryView();
            string viewMode = SmartOps.Web.Bridge.AccountService.IsDiaryView(view) ? view! : savedView;
            if (viewMode != savedView)
                await _accounts.SaveDiaryView(viewMode);
            int displayYear = year ?? now.Year;
            int displayMonth = month ?? now.Month;

            // Calculate the week start (Monday) for week view
            DateTime weekStartDate;
            if (!string.IsNullOrEmpty(weekStart) && DateTime.TryParse(weekStart, out var parsed))
            {
                weekStartDate = parsed;
            }
            else
            {
                // Default to the Monday of the current week
                //int diff = ((int)now.DayOfWeek - 1 + 7) % 7;
                //weekStartDate = now.AddDays(-diff);
                weekStartDate = DateTime.Today;
            }
            DateTime weekEndDate = weekStartDate.AddDays(7);

            var viewModel = new JobIndexViewModel
            {
                Year = displayYear,
                Month = displayMonth,
                ViewMode = viewMode,
                WeekStartDate = weekStartDate
            };

            viewModel.Branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();

            if (UserBranchDefaults.IsLocked(_context, User) || !Request.Query.ContainsKey(nameof(branchId)))
                branchId = UserBranchDefaults.Id(_context, User);
            // Explicit All (0), deleted or inaccessible branches fall back to All.
            viewModel.BranchId = viewModel.Branches.Any(b => b.Id == branchId) ? branchId : null;

            IQueryable<Job> diaryJobs = _context.Job;
            if (viewModel.BranchId.HasValue)
            {
                diaryJobs = diaryJobs.Where(j => j.BranchId == viewModel.BranchId.Value);
            }

            // Keep the legacy All date-range values populated for view-model compatibility
            DateTime defaultAllStart = now.AddMonths(-1);
            DateTime defaultAllEnd = now.AddMonths(1);
            DateTime parsedAllStart;
            DateTime parsedAllEnd;
            if (!string.IsNullOrEmpty(allStart) && DateTime.TryParse(allStart, out var a1))
                parsedAllStart = a1;
            else
                parsedAllStart = defaultAllStart;

            if (!string.IsNullOrEmpty(allEnd) && DateTime.TryParse(allEnd, out var a2))
                parsedAllEnd = a2;
            else
                parsedAllEnd = defaultAllEnd;

            viewModel.AllStartDate = parsedAllStart;
            viewModel.AllEndDate = parsedAllEnd;

            // If a search query is provided, locate matching jobs across the entire DB first
            // and adjust the requested period (month/week) so results are visible.
            List<Job>? matchedJobs = null;
            if (!string.IsNullOrWhiteSpace(q))
            {
                var qnorm = q.Trim();

                matchedJobs = await diaryJobs
                    .Include(j => j.Customer)
                    .Include(j => j.Agent)
                    .Where(j => (!string.IsNullOrEmpty(j.ContractCode) && j.ContractCode.Contains(qnorm))
                                || (j.Customer != null && j.Customer.Name.Contains(qnorm))
                                || (j.Agent != null && j.Agent.Code.Contains(qnorm)))
                    .OrderBy(j => j.DiaryDate)
                    .ToListAsync();

                // Keep search query and matchedJobs but do NOT change the current period when searching.
                // The UI will initially stay on the same month/week and be reduced to matching items.
                if (!matchedJobs.Any())
                {
                    // No matches anywhere: return empty result set with search preserved
                    viewModel.Jobs = new List<Job>();
                    viewModel.SearchQuery = qnorm;
                    return View(viewModel);
                }

                viewModel.SearchQuery = qnorm;
            }

            if (viewMode == "week")
            {
                viewModel.Jobs = await diaryJobs
                    .Include(j => j.Customer)
                    .Include(j => j.Agent)
                    .Include(j => j.Contract)
                    .Where(j =>
                        j.DiaryDate >= weekStartDate &&
                        j.DiaryDate < weekEndDate)
                    .OrderBy(j => j.DiaryDate)
                    .ThenBy(j => j.Time)
                    .ToListAsync();
            }
            else if (viewMode == "list")
            {
                var dayStart = weekStartDate.Date;
                var dayEnd = dayStart.AddDays(1);

                viewModel.Jobs = await diaryJobs
                    .Include(j => j.Customer)
                    .Include(j => j.Agent)
                    .Include(j => j.Contract)
                    .Where(j =>
                        j.DiaryDate >= dayStart &&
                        j.DiaryDate < dayEnd)
                    .ToListAsync();

                // Sort purely by the displayed start time,
                // ignoring any date component stored in Time.
                viewModel.Jobs = viewModel.Jobs
                    .OrderBy(j => j.Time.TimeOfDay)
                    .ThenBy(j => j.ContractCode)
                    .ToList();
            }
            else
            {
                viewModel.Jobs = await diaryJobs
                        .Include(j => j.Customer)
                        .Include(j => j.Agent)
                        .Include(j => j.Contract)
                        .Where(j =>
                            j.DiaryDate.Year == displayYear &&
                            j.DiaryDate.Month == displayMonth)
                        .OrderBy(j => j.DiaryDate)
                        .ThenBy(j => j.Time)
                        .ToListAsync();
            }

            // If we performed a search, reduce the jobs to only those that matched the query
            if (matchedJobs != null && matchedJobs.Any())
            {
                var matchedIds = new HashSet<int>(matchedJobs.Select(m => m.Id));
                viewModel.Jobs = viewModel.Jobs?.Where(j => matchedIds.Contains(j.Id)).ToList() ?? new List<Job>();
            }

            // Always populate Headers with all headers for jobs in view
            if (viewModel.Jobs != null)
            {
                var codes = viewModel.Jobs
                    .Where(j => !string.IsNullOrEmpty(j.ContractCode))
                    .Select(j => j.ContractCode!)
                    .Distinct()
                    .ToList();
                viewModel.Headers = await _context.JobHeader
                    .Where(h => h.ContractCode != null && codes.Contains(h.ContractCode))
                    .ToListAsync();

                var codesWithHeaders = viewModel.Headers
                    .Select(h => h.ContractCode!)
                    .Distinct()
                    .ToList();
                viewModel.ContractCodesWithHeaders = new HashSet<string>(codesWithHeaders);
            }

            return View(viewModel);
        }
        private async Task ValidateBookingAvailabilityAsync(Job job, int? excludeJobId = null, Guid? reservationId = null)
        {
            // All follows the chosen agent when the booking is saved, including without JavaScript.
            if (!job.BranchId.HasValue && !UserBranchDefaults.IsLocked(_context, User))
            {
                job.BranchId = await _context.Agent.Where(a => a.Id == job.AgentId)
                    .Select(a => a.BranchId).FirstOrDefaultAsync();
                if (job.BranchId.HasValue) ModelState.Remove("Job.BranchId");
            }
            if (job.BranchId.HasValue)
            {
                if (!await _context.Branches.AnyAsync(b => b.Id == job.BranchId.Value))
                {
                    ModelState.AddModelError("Job.BranchId", "Choose a branch from your company or All.");
                    return;
                }
                if (!await _context.Agent.AnyAsync(a => a.Id == job.AgentId && a.BranchId == job.BranchId.Value))
                {
                    ModelState.AddModelError("Job.AgentId", "Choose an agent from the selected branch.");
                    return;
                }
            }
            var error = await BookingAvailability.ValidateAsync(_context, job.AgentId,
                job.DiaryDate, job.Time, job.JobTime, excludeJobId, reservationId, User?.FindFirstValue(ClaimTypes.NameIdentifier));
            if (error == null) error = await BookingReservations.CheckForSaveAsync(_context, reservationId, User?.FindFirstValue(ClaimTypes.NameIdentifier), job);
            if (error != null) ModelState.AddModelError("Job.Time", error);
        }

        [HttpGet]
        public async Task<IActionResult> Availability(int agentId, DateTime? date, TimeSpan duration, int? excludeJobId = null, Guid? reservationId = null)
        {
            var first = date?.Date ?? DateTime.Today;
            if (!ModelState.IsValid || first.Year < 2 || first.Year > 9997 ||
                duration <= TimeSpan.Zero || duration >= TimeSpan.FromDays(1))
                return BadRequest("Choose a valid date and a duration greater than zero and less than 24 hours.");
            if (!await _context.Agent.AnyAsync(a => a.Id == agentId)) return NotFound();
            if (excludeJobId.HasValue && !await _context.Job.AnyAsync(j => j.Id == excludeJobId.Value)) return NotFound();
            Response.Headers.CacheControl = "no-store";
            return Json(await BookingAvailability.GetDaysAsync(_context, agentId, first, duration, excludeJobId, reservationId, User?.FindFirstValue(ClaimTypes.NameIdentifier)));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reserve(int agentId, DateTime date, TimeSpan time,
            TimeSpan duration, Guid? previousReservationId = null, int? excludeJobId = null)
        {
            var owner = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (owner == null) return Challenge();
            if (!ModelState.IsValid || date.Year < 2 || date.Year > 9997 ||
                time < TimeSpan.Zero || time >= TimeSpan.FromDays(1) ||
                duration <= TimeSpan.Zero || duration >= TimeSpan.FromDays(1)) return BadRequest();
            var start = date.Date.Add(time);
            if (start < DateTime.Now || time < BookingAvailability.DayStart ||
                time.Add(duration) > BookingAvailability.DayEnd) return BadRequest("Choose an available working time.");
            if (excludeJobId.HasValue && !await _context.Job.AnyAsync(j => j.Id == excludeJobId)) return NotFound();

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var error = await BookingAvailability.ValidateAsync(_context, agentId, date, date.Date.Add(time),
                duration, excludeJobId, previousReservationId, owner);
            if (error != null) return Conflict(new { error });

            var now = DateTime.UtcNow;
            var previous = await BookingReservations.FindOwnedAsync(_context, previousReservationId, owner);
            if (previous != null && previous.ExpiresUtc > now) _context.BookingReservations.Remove(previous);
            // Expired holds do not block availability and can be pruned on the next reservation.
            await _context.BookingReservations.Where(r => r.AgentId == agentId && r.ExpiresUtc <= now).ExecuteDeleteAsync();
            var reservation = new BookingReservation {
                Id = Guid.NewGuid(), UserId = owner, AgentId = agentId,
                Start = start, End = start.Add(duration), ExpiresUtc = now.Add(BookingReservations.HoldTime)
            };
            _context.BookingReservations.Add(reservation);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Json(BookingReservations.Describe(reservation));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseReservation(Guid id)
        {
            var owner = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (owner == null) return Challenge();
            await _context.BookingReservations.Where(r => r.Id == id && r.UserId == owner).ExecuteDeleteAsync();
            return NoContent();
        }


        private void PopulateContractsDropDownList(object? selectedContract = null)
        {
            // All tenant contracts are searchable, independently of their booking status.
            var contracts = _context.Contract.AsNoTracking()
                .OrderByDescending(c => c.ContractNumber)
                .Select(c => new
                {
                    c.Id,
                    c.ContractNumber,
                    c.IncidentDate,
                    CustomerName = c.Customer != null ? c.Customer.Name : ""
                })
                .ToList();

            ViewBag.ContractId = new SelectList(
                contracts.Select(c => new
                {
                    c.Id,
                    DisplayText = c.ContractNumber.ToString("D8") + " - "
                        + c.IncidentDate.ToString("dd/MM/yy") + " - " + c.CustomerName
                }),
                "Id", "DisplayText", selectedContract);
            ViewBag.ContractFilterValues = contracts.ToDictionary(
                c => c.Id.ToString(),
                c => new { number = c.ContractNumber.ToString(), name = c.CustomerName ?? "" });
        }

        private void PopulateFixedBookingContract(Job job, SmartOps.Web.Models.Contract contract)
        {
            ViewData["FixedContractId"] = contract.Id;
            ViewData["BookingContractStatus"] = ItemDisplayNameHelper.GetDisplayName(contract.Status.ToString());
            job.ContractId = contract.Id;
            job.ContractCode = contract.ContractCode;
            job.CustomerId = contract.CustomerId;
            job.Customer = contract.Customer;
            job.InsuranceCompanyName = string.IsNullOrWhiteSpace(contract.Insurer?.Name)
                ? "Non - domestic" : contract.Insurer.Name;
        }

        private void ApplyAuthorizedJobTypeDefault(Job job, SmartOps.Web.Models.Contract? contract)
        {
            if (contract?.Status != ContractStatus.Authorised) return;
            ViewData["DefaultBookingJobTypeChanged"] = job.JobType != enum_job_type.Fitting;
            job.JobType = enum_job_type.Fitting;
        }

        private void PopulateAgentsDropDownList(object? selectedAgent = null, int bookingBranchId = 0)
        {
            ViewBag.BookingAgentBranches = _context.Agent.AsNoTracking()
                .ToDictionary(a => a.Id.ToString(), a => a.BranchId);
            ViewBag.BookingBranches = new SelectList(_context.Branches.AsNoTracking().OrderBy(b => b.Name),
                "Id", "Name", bookingBranchId);
            var agentsQuery = from d in _context.Agent
                              where bookingBranchId <= 0 || d.BranchId == bookingBranchId
                              orderby d.Name
                              select new { d.Id, DisplayText = d.Code + " - " + d.Name };
            ViewBag.AgentId = new SelectList(agentsQuery.AsNoTracking(), "Id", "DisplayText", selectedAgent);
        }

        private void PopulateJobTypeDropDownList(object? selectedValue = null)
        {
            ViewBag.JobTypes = new SelectList(
                Enum.GetValues<enum_job_type>()
                    .Select(j => new { Value = (int)j, Text = j.ToString() }),
                "Value", "Text", selectedValue);
        }

        // GET: Jobs/Details/5
        public async Task<IActionResult> Details(int? id, string? returnUrl = null)
        {
            if (id == null)
                return NotFound();

            var job = await _context.Job
                .Include(j => j.Branch)
                .Include(j => j.Customer)
                .Include(j => j.Agent)
                .Include(j => j.Contract)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound();

            var viewModel = new JobDetailsViewModel
            {
                Job = job,

                ContractUpdates = await _context.ContractUpdates
                    .Where(u => u.ContractId == job.ContractId)
                    .OrderByDescending(u => u.DateTime)
                    .ToListAsync(),

                JobHeaderIndex = await _context.JobHeader
                    .Where(h => h.ContractId == job.ContractId)
                    .OrderByDescending(h => h.DateTimeCompleted)
                    .Select(h => h.AsJobHeaderIndex())
                    .ToListAsync()
            };

            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            return View(viewModel);
        }

        // GET: Jobs/Rebook/5
        [HttpGet]
        public async Task<IActionResult> Rebook(int? id, string? returnUrl = null)
        {
            if (id == null)
                return NotFound();

            var source = await _context.Job
                .AsNoTracking()
                .Include(j => j.Contract)
                    .ThenInclude(c => c!.Customer)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (source?.Contract == null)
                return NotFound();

            var today = DateTime.Today.AddDays(1);
            var job = new Job
            {
                DiaryDate = today,
                Time = today.Add(source.Time.TimeOfDay),
                JobTime = source.JobTime,
                AgentId = source.AgentId,
                BranchId = UserBranchDefaults.IsLocked(_context, User) ? UserBranchDefaults.Id(_context, User) : source.BranchId,
                JobType = source.JobType,
                DamageDesc = source.DamageDesc,
                Instructions = source.Instructions,
                InsuranceCompanyName = source.InsuranceCompanyName,
                CauseOfDamage = source.CauseOfDamage,
                IncidentDate = source.IncidentDate,
                Excess = source.Excess,
                PolicyNumber = source.PolicyNumber,
                bIncludeSurvey = source.bIncludeSurvey,
                bRequestRepudiation = source.bRequestRepudiation
            };

            PopulateFixedBookingContract(job, source.Contract);
            ApplyAuthorizedJobTypeDefault(job, source.Contract);
            PopulateAgentsDropDownList(job.AgentId, job.BranchId ?? 0);
            PopulateJobTypeDropDownList(job.JobType);
            ViewData["PreserveBookingDetails"] = true;
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            return View("Create", new JobDetailsViewModel { Job = job });
        }

        // GET: Jobs/Create
        [HttpGet]
        public IActionResult Create(int? contractId = null, string? returnUrl = null)
        {
            var model = PrepareNewBooking(contractId);
            if (model == null) return NotFound();
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            return View(model);
        }

        private JobDetailsViewModel? PrepareNewBooking(int? contractId = null, DateTime? diaryDate = null)
        {
            JobDetailsViewModel viewModel = new JobDetailsViewModel();

            viewModel.Job = new Job { BranchId = UserBranchDefaults.Id(_context, User) };

            var now = DateTime.Now;

            viewModel.Job.DiaryDate = diaryDate?.Date ?? now.Date;

            var minTime = now.Date.AddHours(7);
            var maxTime = now.Date.AddHours(19);

            viewModel.Job.Time =
                now < minTime ? minTime :
                now > maxTime ? maxTime :
                now;

            // If opening from Contract Details, use that contract.
            if (contractId.HasValue)
            {
                var contract = _context.Contract
                    .Include(c => c.Customer)
                    .Include(c => c.Insurer)
                    .FirstOrDefault(c => c.Id == contractId.Value);

                if (contract == null)
                    return null;

                PopulateFixedBookingContract(viewModel.Job, contract);
                ApplyAuthorizedJobTypeDefault(viewModel.Job, contract);
            }
            else
            {
                PopulateContractsDropDownList();
            }

            PopulateAgentsDropDownList(bookingBranchId: viewModel.Job.BranchId ?? 0);
            PopulateJobTypeDropDownList(viewModel.Job.JobType);

            viewModel.Job.Time = viewModel.Job.DiaryDate.Add(viewModel.Job.Time.TimeOfDay);
            return viewModel;
        }

        // POST: Jobs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
        string? returnUrl,
        [Bind("Id,DiaryDate,Time,JobTime,DamageDesc,Instructions,ContractId,AgentId,BranchId,JobType,bIncludeSurvey,bRequestRepudiation,InsuranceCompanyName,CauseOfDamage")]
        Job job,
        string? contractFilter = null,
        int? fixedContractId = null,
        bool preserveBookingDetails = false, Guid? reservationId = null)
        {
            if (!UserBranchDefaults.AllowsSelection(_context, User, job.BranchId, UserBranchDefaults.Id(_context, User)))
                return BadRequest("Your branch selection is locked.");
            if (fixedContractId.HasValue)
            {
                var chosenContract = await _context.Contract
                    .Include(c => c.Customer)
                    .Include(c => c.Insurer)
                    .FirstOrDefaultAsync(c => c.Id == fixedContractId.Value);
                if (chosenContract == null) return NotFound();
                PopulateFixedBookingContract(job, chosenContract);
                ModelState.Remove("Job.ContractId");
            }

            // Keep the conflict check and insert in one serializable transaction:
            // concurrent requests cannot both reserve the same agent's time.
            await using var bookingTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (ModelState.IsValid)
                await ValidateBookingAvailabilityAsync(job, reservationId: reservationId);

            if (ModelState.IsValid)
            {
                var contract = await _context.Contract
                    .Include(c => c.Insurer)
                    .FirstOrDefaultAsync(c => c.Id == job.ContractId);

                if (contract == null)
                    return NotFound();

                job.ContractCode = contract.ContractCode;
                job.CustomerId = contract.CustomerId;
                job.InsuranceCompanyName = string.IsNullOrWhiteSpace(contract.Insurer?.Name)
                    ? "Non - domestic" : contract.Insurer.Name;
                job.CauseOfDamage = contract.CauseOfDamage;
                job.IncidentDate = contract.IncidentDate;
                job.Excess = contract.Excess;
                job.PolicyNumber = contract.PolicyNumber;

                _context.Add(job);

                // Update the contract status when the diary booking is created
                ContractStatus? newStatus = job.JobType switch
                {
                    enum_job_type.Survey => ContractStatus.SurveyBooked,

                    enum_job_type.Fitting or enum_job_type.Remedial =>
                        ContractStatus.FittingBooked,

                    _ => null
                };

                if (newStatus.HasValue)
                {
                    _context.ContractUpdates.Add(
                        ContractUpdateHelper.Create(
                            contract,
                            newStatus.Value));
                }

                var reservation = await BookingReservations.FindOwnedAsync(_context, reservationId, User?.FindFirstValue(ClaimTypes.NameIdentifier));
                if (reservation != null) _context.BookingReservations.Remove(reservation);
                await _context.SaveChangesAsync();
                await bookingTransaction.CommitAsync();

                // Respect the originating page when a booking was added from Contract Details.
                if (Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);

                return RedirectToAction(nameof(Index), new
                {
                    weekStart = job.DiaryDate.ToString("yyyy-MM-dd"),
                    view = "week"
                });

            }

            ViewData["PreserveBookingDetails"] = preserveBookingDetails;
            ViewBag.ContractFilter = contractFilter;
            if (!fixedContractId.HasValue)
            {
                PopulateContractsDropDownList(job.ContractId);
                var selectedContract = await _context.Contract.AsNoTracking().FirstOrDefaultAsync(c => c.Id == job.ContractId);
                ViewData["BookingContractStatus"] = selectedContract == null ? "" : ItemDisplayNameHelper.GetDisplayName(selectedContract.Status.ToString());
            }

            PopulateAgentsDropDownList(
                job.AgentId, job.BranchId ?? 0);

            PopulateJobTypeDropDownList(
                job.JobType);

            JobDetailsViewModel viewModel = new JobDetailsViewModel
            {
                Job = job
            };

            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
            ViewData["BookingReservation"] = await BookingReservations.FindOwnedAsync(_context, reservationId, User?.FindFirstValue(ClaimTypes.NameIdentifier));
            return View(viewModel);
        }

        // GET: Jobs/Edit/5
        public async Task<IActionResult> Edit(int? id, string? returnUrl = null)
        {
            if (id == null)
                return NotFound();

            var job = await _context.Job
                .AsNoTracking()
                .Include(j => j.Customer)
                .Include(j => j.Contract)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound();

            var viewModel = new JobDetailsViewModel
            {
                Job = job
            };

            ViewData["BookingContractStatus"] = job.Contract == null ? "" : ItemDisplayNameHelper.GetDisplayName(job.Contract.Status.ToString());
            ApplyAuthorizedJobTypeDefault(job, job.Contract);
            PopulateAgentsDropDownList(job.AgentId, job.BranchId ?? 0);
            PopulateJobTypeDropDownList(job.JobType);
            ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string? returnUrl, [Bind("Id,DiaryDate,Time,JobTime,Instructions,AgentId,BranchId,JobType,bIncludeSurvey,bRequestRepudiation")] Job job, Guid? reservationId = null)
        {
            if (id != job.Id)
                return NotFound();

            // These fields are not edited on this page
            ModelState.Remove("Job.InsuranceCompanyName");
            ModelState.Remove("Job.DamageDesc");
            ModelState.Remove("Job.CauseOfDamage");
            ModelState.Remove("Job.ContractCode");

            await using var bookingTransaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var existingJob = await _context.Job
                .Include(j => j.Customer)
                .Include(j => j.Contract)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (existingJob == null)
                return NotFound();
            if (!UserBranchDefaults.AllowsSelection(_context, User, job.BranchId, existingJob.BranchId))
                return BadRequest("Your branch selection is locked.");

            if (ModelState.IsValid)
                await ValidateBookingAvailabilityAsync(job, existingJob.Id, reservationId);

            if (!ModelState.IsValid)
            {
                job.ContractId = existingJob.ContractId;
                job.ContractCode = existingJob.ContractCode;
                job.CustomerId = existingJob.CustomerId;
                job.Customer = existingJob.Customer;
                job.DamageDesc = existingJob.DamageDesc;
                job.CauseOfDamage = existingJob.CauseOfDamage;
                job.InsuranceCompanyName = existingJob.InsuranceCompanyName;
                ViewData["BookingContractStatus"] = existingJob.Contract == null ? "" : ItemDisplayNameHelper.GetDisplayName(existingJob.Contract.Status.ToString());
                PopulateAgentsDropDownList(job.AgentId, job.BranchId ?? 0);
                PopulateJobTypeDropDownList(job.JobType);
                ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

                ViewData["BookingReservation"] = await BookingReservations.FindOwnedAsync(_context, reservationId, User?.FindFirstValue(ClaimTypes.NameIdentifier));
                return View(new JobDetailsViewModel
                {
                    Job = job
                });
            }

            existingJob.DiaryDate = job.DiaryDate;
            existingJob.Time = job.Time;
            existingJob.JobTime = job.JobTime;
            existingJob.Instructions = job.Instructions;
            existingJob.AgentId = job.AgentId;
            existingJob.BranchId = job.BranchId;
            existingJob.JobType = job.JobType;
            existingJob.bIncludeSurvey = job.bIncludeSurvey;
            existingJob.bRequestRepudiation = job.bRequestRepudiation;

            var reservation = await BookingReservations.FindOwnedAsync(_context, reservationId, User?.FindFirstValue(ClaimTypes.NameIdentifier));
            if (reservation != null) _context.BookingReservations.Remove(reservation);
            await _context.SaveChangesAsync();
            await bookingTransaction.CommitAsync();

            return RedirectToAction(
                nameof(Details),
                new
                {
                    id,
                    returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
                });
        }

        // GET: Jobs/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Job == null)
            {
                return NotFound();
            }

            var job = await _context.Job
                .Include(j => j.Customer)
                .Include(j => j.Agent)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (job == null)
            {
                return NotFound();
            }

            return View(job);
        }

        // POST: Jobs/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var job = await _context.Job
                .Include(j => j.Contract)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
                return NotFound();

            var contract = job.Contract;

            if (contract != null)
            {
                var start = job.Time;
                var end = start.Add(job.JobTime);

                var description =
                    $"{job.JobType} booking cancelled - " +
                    $"{job.DiaryDate:dd/MM/yyyy}, " +
                    $"{start:HH:mm} - {end:HH:mm}";

                _context.ContractUpdates.Add(new ContractUpdate
                {
                    ContractId = contract.Id,
                    DateTime = DateTime.UtcNow,
                    Description = description
                });
            }

            _context.Job.Remove(job);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool JobExists(int id)
        {
            return (_context.Job?.Any(e => e.Id == id)).GetValueOrDefault();
        }

        [HttpGet]
        public async Task<IActionResult> GetContractInformation(int id)
        {
            var contract = await _context.Contract
                .Include(c => c.Customer)
                .Include(c => c.Insurer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contract == null)
            {
                return Json(new
                {
                    damageDescription = "",
                    causeOfDamage = "",
                    insuranceCompany = "",
                    customerAddress = "",
                    contractStatus = (int?)null,
                    contractStatusText = ""
                });
            }

            return Json(new
            {
                damageDescription = contract.DamageDescription ?? "",
                causeOfDamage = contract.CauseOfDamage ?? "",
                insuranceCompany = string.IsNullOrWhiteSpace(contract.Insurer?.Name)
                    ? "Non - domestic" : contract.Insurer.Name,
                customerAddress = string.Join(", ", new[] { contract.Customer?.Add1, contract.Customer?.Add2,
                    contract.Customer?.Add3, contract.Customer?.Postcode }.Where(part => !string.IsNullOrWhiteSpace(part))),
                contractStatus = (int)contract.Status,
                contractStatusText = ItemDisplayNameHelper.GetDisplayName(contract.Status.ToString())
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRecentSurvey(int id)
        {
            var contract = await _context.Contract.FindAsync(id);
            if (contract == null)
            {
                return Json(new { hasSurvey = false });
            }
            JobHeaderIndex? jobHeaderIndexFirst = await _context.JobHeader
                                    .Where(h => h.ContractId == contract.Id && h.JobType == enum_job_type.Survey)
                                    .OrderByDescending(h => h.DateTimeCompleted)
                                    .ThenByDescending(h => h.Id)
                                    .Select(h => h.AsJobHeaderIndex())
                                    .FirstOrDefaultAsync();
            return Json(new { hasSurvey = jobHeaderIndexFirst != null, agentCode = jobHeaderIndexFirst?.AgentCode, diaryDate = jobHeaderIndexFirst?.DiaryDate.ToShortDateString(), completed = jobHeaderIndexFirst?.DateTimeCompleted.ToString() });
        }

        [HttpGet]
        public async Task<IActionResult> GetAgentListForJobType(enum_job_type jobType, int? branchId = null)
        {
            if (UserBranchDefaults.IsLocked(_context, User))
                branchId = UserBranchDefaults.Id(_context, User);
            // Filter your agents based on job type
            var agents = (jobType == enum_job_type.Fitting || jobType == enum_job_type.Remedial)
                ? _context.Agent.Where(a => a.Type == agent_type.Fitter || a.Type == agent_type.Satellite)
                : _context.Agent.Where(a => a.Type == agent_type.Surveyor || a.Type == agent_type.Satellite);

            if (branchId > 0)
            {
                agents = agents.Where(a => a.BranchId == branchId);
            }

            var selectList = await agents.AsNoTracking().OrderBy(a => a.Name)
                .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Code + " - " + a.Name })
                .ToListAsync();

            return Json(selectList);
        }

    }
}
