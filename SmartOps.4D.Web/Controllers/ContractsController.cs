using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using SmartOps.Web.Models;
using SmartOps.Web.Options;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using SmartOps.Web.Helpers;
[Microsoft.AspNetCore.Authorization.Authorize(Roles = SmartOps.Web.Helpers.StaffRoles.ManageDiary)]
    public class ContractsController : Controller
{

    private readonly AppDBContext _context;
    private readonly IEmailSender _emailSender;
    private readonly UserManager<AppUser> _userManager;
    private readonly SmartOps.Web.Bridge.AccountService _accounts;

    public ContractsController(
        AppDBContext context,
        IEmailSender emailSender,
        UserManager<AppUser> userManager,
        SmartOps.Web.Bridge.AccountService accounts)
    {
        _context = context;
        _userManager = userManager;
        _emailSender = emailSender;
        _accounts = accounts;
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult GeneratePassword() => Json(new { password = TemporaryUserPassword.Generate() });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLayout(bool compactList, string? search, string? status,
        string active = "active", int page = 1)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();
        await _accounts.SaveContractLayout(compactList);
        return RedirectToAction(nameof(Index), new { search, status, active, page });
    }

    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string active = "active",
        int page = 1)
    {
        const int pageSize = 10;

        ViewBag.CompactList = await _accounts.GetContractLayout();

        var query = _context.Contract
            .AsNoTracking()
            .Include(c => c.Customer)
            .Include(c => c.Insurer)
            .AsQueryable();

        // Active / inactive
        if (active == "active")
        {
            query = query.Where(c => c.IsActive);
        }
        else if (active == "inactive")
        {
            query = query.Where(c => !c.IsActive);
        }

        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            bool isContractNumber =
                int.TryParse(search, out int contractNumber);

            query = query.Where(c =>
                (isContractNumber && c.ContractNumber == contractNumber) ||
                c.Customer.Name.Contains(search) ||
                c.Customer.Postcode.Contains(search) ||
                c.PolicyNumber.Contains(search));
        }

        // Status
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ContractStatus>(status, out var contractStatus))
        {
            query = query.Where(c =>
                c.Status == contractStatus);
        }

        var totalItems = await query.CountAsync();

        var totalPages =
            (int)Math.Ceiling(totalItems / (double)pageSize);

        page = Math.Clamp(page, 1, Math.Max(1, totalPages));

        var contracts = await query
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get jobs for the contracts on this page
        var contractIds = contracts
            .Select(c => c.Id)
            .ToList();

        var jobs = await _context.Job
            .AsNoTracking()
            .Include(j => j.Agent)
            .Where(j =>
                contractIds.Contains(j.ContractId) &&
                j.DiaryDate >= DateTime.Today)
            .ToListAsync();

        // Match reports by contract code, as on the diary, including past appointments.
        var contractCodes = contracts.Select(c => c.ContractCode).ToList();
        var headers = await _context.JobHeader
            .AsNoTracking()
            .Where(h => contractCodes.Contains(h.ContractCode))
            .ToListAsync();
        var headersByContract = headers.ToLookup(h => h.ContractCode);

        // Build the view model
        var model = contracts
            .Select(contract => new ContractIndexViewModel
            {
                Contract = contract,
                Headers = headersByContract[contract.ContractCode].ToList(),

                NextJob = jobs
                    .Where(j => j.ContractId == contract.Id)
                    .OrderBy(j => j.DiaryDate)
                    .ThenBy(j => j.Time)
                    .FirstOrDefault()
            })
            .ToList();

        ViewBag.PageNumber = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalItems = totalItems;

        ViewBag.Search = search;
        ViewBag.Status = status;
        ViewBag.Active = active;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCustomerLogin(
        int contractId,
        int customerId,
        string customerEmail,
        string issuedPassword,
        bool returnToCustomerDetails = false,
        string? returnUrl = null)
    {
        var contract = await _context.Contract
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x =>
                x.Id == contractId &&
                x.CustomerId == customerId &&
                (!returnToCustomerDetails || (x.IsActive && !x.IsCancelled)));

        if (contract?.Customer == null)
            return NotFound();

        var customer = contract.Customer;

        customer.Email =
            customerEmail.Trim().ToLowerInvariant();

        // Has the issued password been changed?
        if (customer.IssuedPassword != issuedPassword)
        {
            customer.IssuedPassword = issuedPassword;

            var hasher = new PasswordHasher<Customer>();

            customer.PasswordHash =
                hasher.HashPassword(
                    customer,
                    issuedPassword);

            // Existing customer app login is no longer valid
            customer.AuthenticationString = "";
        }

        await _context.SaveChangesAsync();

        if (returnToCustomerDetails)
            TempData["Success"] = "Login details saved.";

        return RedirectAfterCustomerLogin(contractId, customerId, returnToCustomerDetails, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmailCustomerLogin(
        int contractId,
        int customerId,
        string customerEmail,
        string issuedPassword,
        bool returnToCustomerDetails = false,
        string? returnUrl = null)
    {
        var contract = await _context.Contract
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x =>
                x.Id == contractId &&
                x.CustomerId == customerId &&
                (!returnToCustomerDetails || (x.IsActive && !x.IsCancelled)));

        if (contract?.Customer == null)
            return NotFound();

        var customer = contract.Customer;

        // Update email
        customer.Email =
            customerEmail.Trim().ToLowerInvariant();

        // If the issued password has changed,
        // make it the customer's new login password.
        if (customer.IssuedPassword != issuedPassword)
        {
            customer.IssuedPassword = issuedPassword;

            var hasher = new PasswordHasher<Customer>();

            customer.PasswordHash =
                hasher.HashPassword(
                    customer,
                    issuedPassword);

            // Force customer app to log in again
            customer.AuthenticationString = "";
        }

        // SAVE FIRST
        await _context.SaveChangesAsync();

        // THEN EMAIL THE SAVED DETAILS
        var subject = "Your Mart Ops login details";

        var body = $"""
    <p>Hello {customer.Name},</p>

    <p>Your Mart Ops customer login details are:</p>

    <p>
        <strong>Reference:</strong> {contract.CustomerReference}<br />
        <strong>Email:</strong> {customer.Email}<br />
        <strong>Password:</strong> {customer.IssuedPassword}
    </p>

    <p>
        Download the Mart Ops Customer app from Google Play:
    </p>

    <p>
        <a href="https://play.google.com/store/apps/details?id=uk.co.glassops.customer"
           style="display:inline-block;
                  padding:10px 18px;
                  background:#03386C;
                  color:white;
                  text-decoration:none;
                  border-radius:5px;">
            Download from Google Play
        </a>
    </p>

    <p>
        Once installed, use the details above to sign in and
        view your glazing repair.
    </p>

    <p>
        Kind regards,<br />
        Mart Ops
    </p>
    """;

        await _emailSender.SendEmailAsync(
            customer.Email,
            subject,
            body);

        TempData["Success"] =
            $"Login details saved and emailed to {customer.Email}.";

        return RedirectAfterCustomerLogin(contractId, customerId, returnToCustomerDetails, returnUrl);
    }


    private IActionResult RedirectAfterCustomerLogin(
        int contractId, int customerId, bool returnToCustomerDetails, string? returnUrl)
    {
        if (returnToCustomerDetails)
        {
            return RedirectToAction(nameof(Details), "Customers",
                new { id = customerId, contractId, returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
        }

        return RedirectToAction(nameof(Details), new { id = contractId, tab = "customer" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
            return BadRequest("Enter a note.");

        var contract = await _context.Contract.FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null)
            return NotFound();

        _context.ContractNotes.Add(new ContractNote
        {
            ContractCode = contract.ContractCode,
            DateAdded = DateTime.Now,
            Note = note,
            AddedBy = User.Identity?.Name ?? ""
        });
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id, tab = "notes" });
    }

    // GET: CONTRACTS/Details/5
    public async Task<IActionResult> Details(int? id, string? returnUrl = null)
    {
        if (id == null)
            return NotFound();

        var contract = await _context.Contract
            .Include(c => c.Customer)
            .Include(c => c.Insurer)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (contract == null)
            return NotFound();

        var nextJob = await _context.Job
            .Where(j =>
                j.ContractId == contract.Id &&
                j.DiaryDate >= DateTime.Today)
            .OrderBy(j => j.DiaryDate)
            .ThenBy(j => j.Time)
            .FirstOrDefaultAsync();

        var unreadInsurerMessages = await _context.InsurerMessages
            .CountAsync(x =>
                x.ContractId == contract.Id &&
                x.Sender == InsurerMessageSender.Insurer &&
                !x.ReadByCompany);

        ViewBag.UnreadInsurerMessages =
            unreadInsurerMessages;

        await _context.AuditBulkAsync("InsurerMessages", contract.TenantId, "Updated", () => _context.InsurerMessages
            .Where(x =>
                x.ContractId == contract.Id &&
                x.Sender == InsurerMessageSender.Insurer &&
                !x.ReadByCompany)
            .ExecuteUpdateAsync(x =>
                x.SetProperty(
                    m => m.ReadByCompany,
                    true)));

        var viewModel = new ContractDetailsViewModel
        {
            Contract = contract,

            ContractUpdates = await _context.ContractUpdates
            .Where(x => x.ContractId == contract.Id)
            .OrderByDescending(x => x.DateTime)
            .ToListAsync(),

            ContractNotes = await _context.ContractNotes
            .Where(n =>
                n.ContractCode == contract.ContractCode)
            .OrderByDescending(n => n.DateAdded)
            .ToListAsync(),

                SupportTickets = await _context.SupportTickets
            .Include(t => t.Messages)
                .ThenInclude(m => m.SentByUser)
            .Where(t => t.ContractId == contract.Id)
            .OrderByDescending(t => t.Created)
            .ToListAsync(),

                InsurerMessages = await _context.InsurerMessages
            .Include(x => x.SentByUser)
            .Where(x => x.ContractId == contract.Id)
            .OrderBy(x => x.Created)
            .ToListAsync(),

            NextJob = nextJob,

            InvoiceId = await _context.Invoices.AsNoTracking().Where(i => i.ContractId == contract.Id).Select(i => (int?)i.Id).FirstOrDefaultAsync(),

            Estimates = await _context.Estimates.AsNoTracking()
                .Include(e => e.Lines)
                .Where(e => e.ContractId == contract.Id)
                .OrderByDescending(e => e.CreatedUtc).ThenByDescending(e => e.Id)
                .ToListAsync(),

            JobsReceived = await _context.JobHeader
                .AsNoTracking()
                .Where(h => h.ContractCode == contract.ContractCode)
                .OrderByDescending(h => h.DateTimeCompleted)
                .ThenByDescending(h => h.Id)
                .ToListAsync(),

            Bookings = await _context.Job
                .Include(j => j.Agent)
                .Where(j => j.ContractId == contract.Id)
                .OrderByDescending(j => j.DiaryDate)
                .ThenByDescending(j => j.Time)
                .ToListAsync()
        };

        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(viewModel);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToTicket(
    int ticketId,
    int contractId,
    string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return RedirectToAction("Details", new { id = contractId });

        var ticket = await _context.SupportTickets
            .FirstOrDefaultAsync(x => x.Id == ticketId);

        if (ticket == null)
            return NotFound();

        var reply = new SupportTicketMessage
        {
            TenantId = ticket.TenantId,

            SupportTicketId = ticket.Id,

            Message = message.Trim(),

            Sender = TicketMessageSender.HeadOffice,

            SentByUserId = _userManager.GetUserId(User),

            Created = DateTime.UtcNow,

            ReadByOffice = true,
            ReadByCustomer = false
        };

        _context.SupportTicketMessages.Add(reply);

        ticket.Status = TicketStatus.AwaitingCustomer;

        await _context.SaveChangesAsync();

        return RedirectToAction(
        nameof(Details),
        new
        {
            id = contractId,
            tab = "customer"
        });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTrafficLight(
        int id, SmartOps.Shared.Enums.enum_tlight? trafficLight,
        string? tab = null, string? returnUrl = null)
    {
        if (!ModelState.IsValid || trafficLight == null || !Enum.IsDefined(trafficLight.Value))
            return BadRequest("Choose Red, Orange or Green.");

        var contract = await _context.Contract.FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null)
            return NotFound();

        contract.TrafficLight = trafficLight.Value;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new
        {
            id,
            tab,
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        int id, ContractStatus? status, string? returnUrl = null)
    {
        if (!ModelState.IsValid || status == null || !Enum.IsDefined(status.Value))
            return BadRequest("Choose a valid contract status.");

        var contract = await _context.Contract.FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null)
            return NotFound();
        if (contract.IsCancelled)
            return BadRequest("A cancelled contract's status cannot be changed.");

        if (contract.Status != status.Value)
        {
            var previousStatus = contract.Status;
            var update = ContractUpdateHelper.Create(contract, status.Value);
            update.Description = $"Status changed from {ContractStatusHelper.GetTitle(previousStatus)} to {ContractStatusHelper.GetTitle(status.Value)}.";
            _context.ContractUpdates.Add(update);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Details), new
        {
            id,
            tab = "updates",
            returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    // GET: CONTRACTS/Create
    public IActionResult Create(string? returnUrl = null)
    {
        var contract = new Contract
        {
            Status = ContractStatus.Reported
        };

        ViewData["CreateNewCustomer"] = false;
        ViewData["NewCustomer"] = new InlineContractCustomerViewModel();
        PopulateDropDowns();
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        return View(contract);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string? returnUrl,
        [Bind("CustomerId,Status,TrafficLight,IncidentDate,DamageDescription,CauseOfDamage,Excess,PolicyNumber,InsurerId")]
        Contract contract,
        [Bind(Prefix = "NewCustomer")] InlineContractCustomerViewModel newCustomer,
        bool createNewCustomer = false,
        string? customerFilter = null)
    {
        PrepareDomesticContract(contract);
        if (createNewCustomer)
        {
            // The existing-customer selector is disabled in this mode.
            ModelState.Remove(nameof(Contract.CustomerId));
            TryValidateModel(newCustomer, "NewCustomer");
        }
        else
        {
            // New-customer fields are not used when choosing an existing customer.
            foreach (var key in ModelState.Keys.Where(key =>
                key.Equals("NewCustomer", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("NewCustomer.", StringComparison.OrdinalIgnoreCase)).ToList())
                ModelState.Remove(key);

            if (contract.CustomerId <= 0 || !await _context.Customer.AnyAsync(c => c.Id == contract.CustomerId))
                ModelState.AddModelError(nameof(Contract.CustomerId), "Choose an existing customer.");
        }

        if (ModelState.IsValid)
        {
            if (createNewCustomer)
            {
                var customer = new Customer
                {
                    Name = newCustomer.Name!.Trim(),
                    Add1 = newCustomer.Add1!.Trim(),
                    Add2 = newCustomer.Add2?.Trim(),
                    Add3 = newCustomer.Add3?.Trim(),
                    Postcode = newCustomer.Postcode!.Trim(),
                    Phone1 = newCustomer.Phone1?.Trim() ?? "",
                    Phone2 = newCustomer.Phone2?.Trim(),
                    Phone3 = newCustomer.Phone3?.Trim(),
                    Email = newCustomer.Email?.Trim().ToLowerInvariant() ?? "",
                    IsActive = true
                };
                contract.CustomerId = 0;
                contract.Customer = customer;
            }

            int nextContractNumber =
                (await _context.Contract.IgnoreQueryFilters()
                    .MaxAsync(c => (int?)c.ContractNumber) ?? 0) + 1;
            contract.ContractNumber = nextContractNumber;
            contract.CustomerReference = CustomerReferenceHelper.Encode(nextContractNumber);
            var update = new ContractUpdate
            {
                Contract = contract,
                DateTime = DateTime.Now,
                Status = contract.Status,
                Title = ContractStatusHelper.GetTitle(contract.Status),
                Description = ContractStatusHelper.GetDescription(contract.Status),
                CustomerVisible = true
            };

            // One save inserts the new customer, its contract and status history atomically.
            _context.Contract.Add(contract);
            _context.ContractUpdates.Add(update);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new
            {
                id = contract.Id,
                returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
            });
        }

        ViewData["CreateNewCustomer"] = createNewCustomer;
        ViewData["NewCustomer"] = newCustomer;
        ViewData["CustomerFilter"] = customerFilter;
        PopulateDropDowns(contract.CustomerId, contract.InsurerId, contract.CauseOfDamage);
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(contract);
    }

    // GET: CONTRACTS/Edit/5
    public async Task<IActionResult> Edit(int? id, string? returnUrl = null)
    {
        if (id == null)
            return NotFound();

        var contract = await _context.Contract
            .FirstOrDefaultAsync(x => x.Id == id);

        if (contract == null)
            return NotFound();

        PopulateDropDowns(
                contract.CustomerId,
                contract.InsurerId,
                contract.CauseOfDamage);
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        return View(contract);
    }

    // POST: CONTRACTS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
    int? id,
    string? returnUrl,
    [Bind("Id,ContractCode,CustomerId,Status,TrafficLight,IncidentDate,DamageDescription,CauseOfDamage,InsurerId,PolicyNumber,Excess")]
    Contract contract)
    {
        if (id != contract.Id)
            return NotFound();
        /*
        var errors = ModelState
    .Where(x => x.Value?.Errors.Count > 0)
    .Select(x => new
    {
        Field = x.Key,
        Errors = x.Value!.Errors
            .Select(e => e.ErrorMessage)
            .ToList()
    })
    .ToList();
        */
        PrepareDomesticContract(contract);
        if (ModelState.IsValid)
        {
            try
            {
                var existingContract = await _context.Contract
                                                .FirstOrDefaultAsync(x => x.Id == id);

                if (existingContract == null)
                    return NotFound();

                bool statusChanged =
                    existingContract.Status != contract.Status;

                existingContract.CustomerId =
                    contract.CustomerId;

                existingContract.IncidentDate =
                    contract.IncidentDate;

                existingContract.DamageDescription =
                    contract.DamageDescription;

                existingContract.CauseOfDamage =
                    contract.CauseOfDamage;

                existingContract.InsurerId =
                    contract.InsurerId;

                existingContract.PolicyNumber =
                    contract.PolicyNumber;
                existingContract.Excess = contract.Excess;

                existingContract.Status =
                    contract.Status;

                existingContract.TrafficLight = contract.TrafficLight;

                if (statusChanged)
                {
                    ContractUpdate update = new ContractUpdate
                    {
                        ContractId = existingContract.Id,
                        DateTime = DateTime.Now,
                        Status = existingContract.Status,
                        Title = ContractStatusHelper.GetTitle(existingContract.Status),
                        Description = ContractStatusHelper.GetDescription(existingContract.Status),
                        CustomerVisible = true
                    };

                    _context.ContractUpdates.Add(update);
                }

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ContractExists(contract.Id))
                    return NotFound();

                throw;
            }

            return RedirectToAction(
                nameof(Details),
                new {
                    id = contract.Id,
                    returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
                });
        }

        PopulateDropDowns(
            contract.CustomerId,
            contract.InsurerId,
            contract.CauseOfDamage);
        ViewBag.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        return View(contract);
    }



    // GET: CONTRACTS/Delete/5
    public async Task<IActionResult> Cancel(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var contract = await _context.Contract
            .FirstOrDefaultAsync(m => m.Id == id);
        if (contract == null)
        {
            return NotFound();
        }

        return View(contract);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelConfirmed(
        int id,
        string cancellationReason)
    {
        var contract = await _context.Contract
            .FirstOrDefaultAsync(x => x.Id == id);

        if (contract == null)
            return NotFound();

        if (contract.IsCancelled)
            return RedirectToAction(nameof(Details), new { id });

        if (string.IsNullOrWhiteSpace(cancellationReason))
        {
            ModelState.AddModelError(
                "cancellationReason",
                "Please enter a reason for cancellation.");

            return View("Cancel", contract);
        }

        // Mark the contract as cancelled.
        contract.IsActive = false;
        contract.IsCancelled = true;
        contract.CancelledAt = DateTime.UtcNow;
        contract.CancellationReason = cancellationReason.Trim();

        // Record the cancellation in the contract history.
        _context.ContractUpdates.Add(new ContractUpdate
        {
            ContractId = contract.Id,
            TenantId = contract.TenantId,
            DateTime = DateTime.Now,
            Status = contract.Status,
            Title = "Contract Cancelled",
            Description = contract.CancellationReason,
            CustomerVisible = true
        });

        // Remove future diary bookings.
        var futureJobs = await _context.Job
            .Where(j =>
                j.ContractId == contract.Id &&
                j.DiaryDate >= DateTime.Today)
            .ToListAsync();

        _context.Job.RemoveRange(futureJobs);

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id });
    }

    private void PrepareDomesticContract(Contract contract)
    {
        if (contract.InsurerId.HasValue) return;
        contract.PolicyNumber ??= "";
        contract.Excess ??= "";
        foreach (var key in ModelState.Keys.Where(k => k.Equals(nameof(Contract.PolicyNumber), StringComparison.OrdinalIgnoreCase)
            || k.Equals(nameof(Contract.Excess), StringComparison.OrdinalIgnoreCase)
            || k.EndsWith(".PolicyNumber", StringComparison.OrdinalIgnoreCase)
            || k.EndsWith(".Excess", StringComparison.OrdinalIgnoreCase)).ToList())
            ModelState.Remove(key);
    }

    private bool ContractExists(int? id)
    {
        return _context.Contract.Any(e => e.Id == id);
    }
    private void PopulateDropDowns(
     object? selectedCustomer = null,
     object? selectedInsurer = null,
     string? selectedCauseOfDamage = null)
    {
        var customers = _context.Customer.AsNoTracking()
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new { c.Id, c.Name, c.Postcode }).ToList();
        ViewData["CustomerSearchValues"] = customers.ToDictionary(
            c => c.Id.ToString(), c => c.Name + " " + c.Postcode);

        ViewBag.Customers = new SelectList(
            customers.Select(c => new
            {
                c.Id,
                DisplayText = string.IsNullOrWhiteSpace(c.Postcode) ? c.Name : c.Name + " — " + c.Postcode
            }),
            "Id",
            "DisplayText",
            selectedCustomer);

        var insurers = _context.Insurers
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name });

        ViewBag.Insurers = new SelectList(
            insurers,
            "Id",
            "Name",
            selectedInsurer);

        ViewBag.CausesOfDamage = new SelectList(
            CauseOfDamageOptions.All,
            selectedCauseOfDamage);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyToInsurer(
    int contractId,
    string insurerMessage)
    {
        if (string.IsNullOrWhiteSpace(insurerMessage))
        {
            return RedirectToAction(
                nameof(Details),
                new { id = contractId });
        }

        var contract = await _context.Contract
            .FirstOrDefaultAsync(x => x.Id == contractId);

        if (contract == null)
            return NotFound();

        if (!contract.InsurerId.HasValue)
            return BadRequest();

        var message = new InsurerMessage
        {
            ContractId = contract.Id,
            InsurerId = contract.InsurerId.Value,

            Message = insurerMessage.Trim(),

            Sender = InsurerMessageSender.Company,

            SentByUserId = _userManager.GetUserId(User),

            Created = DateTime.UtcNow,

            ReadByCompany = true,
            ReadByInsurer = false
        };

        _context.InsurerMessages.Add(message);

        await _context.SaveChangesAsync();

        return RedirectToAction(
                nameof(Details),
                new
                {
                    id = contractId,
                    tab = "insurer"
                });
    }

}
