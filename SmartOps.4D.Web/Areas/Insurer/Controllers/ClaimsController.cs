using SmartOps.Web.Areas.Insurer.ViewModels;
using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using SmartOps.Web.Models;
using SmartOps.Web.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Configuration;


[Area("Insurer")]
[Authorize(Roles = "Insurer")]
public class ClaimsController : Controller
{
    private readonly AppDBContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _configuration;
    public ClaimsController(
        AppDBContext context,
        UserManager<AppUser> userManager,
        IConfiguration configuration)
    {
        _context = context;
        _userManager = userManager;
        _configuration = configuration;
    }


    public async Task<IActionResult> Index(int page = 1)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        var claims = await _context.InsuranceClaims
            .Where(x => x.InsurerId == user.InsurerId.Value)
            .OrderByDescending(x => x.Created)
            .ThenByDescending(x => x.Id)
            .Select(x => new InsuranceClaimListItemViewModel
            {
                Id = x.Id,

                ClaimReference = x.ClaimReference,

                CustomerName = x.CustomerName,

                Postcode = x.Postcode,

                IncidentDate = x.IncidentDate,

                Status = x.Status,
                
                BidCount = _context.InsuranceClaimRequests.Count(r => r.InsuranceClaimId == x.Id),

                Created = x.Created,

                AssignedTenantName = x.AssignedTenantId.HasValue
                    ? _context.Tenant
                        .IgnoreQueryFilters()
                        .Where(t => t.Id == x.AssignedTenantId.Value)
                        .Select(t => t.Name)
                        .FirstOrDefault() ?? ""
                    : "",

                ContractId = _context.Contract
                    .IgnoreQueryFilters()
                    .Where(c => c.InsuranceClaimId == x.Id)
                    .Select(c => (int?)c.Id)
                    .FirstOrDefault(),

                ContractCode = _context.Contract
                    .IgnoreQueryFilters()
                    .Where(c => c.InsuranceClaimId == x.Id)
                    .Select(c => c.ContractCode)
                    .FirstOrDefault() ?? ""
            })
            .ToRecordPageAsync(page, ViewData);

        return View(claims);
    }




    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        await PopulateGlazingCompanies();

        ViewBag.CausesOfDamage =
                    new SelectList(CauseOfDamageOptions.All);

        return View(new InsuranceClaimCreateViewModel
        {
            IncidentDate = DateTime.Today
        });
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
    InsuranceClaimCreateViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateGlazingCompanies();

            ViewBag.CausesOfDamage =
                new SelectList(CauseOfDamageOptions.All);

            return View(model);
        }

        var claim = new InsuranceClaim
        {
            InsurerId = user.InsurerId.Value,

            ClaimReference = model.ClaimReference.Trim(),
            CustomerName = model.CustomerName.Trim(),
            Address1 = model.Address1.Trim(),

            Address2 = model.Address2 ?? "",
            Address3 = model.Address3 ?? "",
            Postcode = model.Postcode.Trim(),

            Telephone = model.Telephone ?? "",
            Email = model.Email ?? "",

            IncidentDate = model.IncidentDate,

            CauseOfDamage = model.CauseOfDamage,

            DamageDescription = model.DamageDescription ?? "",

            PolicyNumber = model.PolicyNumber ?? "",

            Excess = model.Excess,

            AssignedTenantId = model.AssignedTenantId,

            Status = model.AddToJobBoard
                ? InsuranceClaimStatus.OnJobBoard
                : model.AssignedTenantId.HasValue
                    ? InsuranceClaimStatus.Assigned
                    : InsuranceClaimStatus.New,

            AssignedDate =
                model.AssignedTenantId.HasValue
                    ? DateTime.UtcNow
                    : null
        };

        _context.InsuranceClaims.Add(claim);

        AddClaimUpdate(
                claim,
                claim.Status,
                "Claim created",
                "The insurance claim was created.");

        await _context.SaveChangesAsync();

        await SaveClaimPhotosAsync(
            claim.Id,
            model.Photos);

        if (model.AssignedTenantId.HasValue)
        {
            await AssignClaimToTenantAsync(
                claim,
                model.AssignedTenantId.Value);
        }

        return RedirectToAction(nameof(Details),
            new { id = claim.Id });
    }

    private async Task SaveClaimPhotosAsync(
     int insuranceClaimId,
     List<IFormFile> photos)
    {
        if (photos.Count == 0)
            return;

        string[] allowedExtensions =
        {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

        const long maxFileSize =
            10 * 1024 * 1024;

        var imageStoragePath =
            _configuration["ImageStoragePath"]
            ?? throw new InvalidOperationException(
                "ImageStoragePath is not configured.");

        var claimFolder = Path.Combine(
            imageStoragePath,
            "Insurer",
            insuranceClaimId.ToString());

        Directory.CreateDirectory(claimFolder);

        foreach (var photo in photos)
        {
            if (photo.Length == 0 ||
                photo.Length > maxFileSize)
                continue;

            var extension =
                Path.GetExtension(photo.FileName)
                    .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                continue;

            var filename =
                $"{Guid.NewGuid():N}{extension}";

            var fullPath = Path.Combine(
                claimFolder,
                filename);

            await using (var stream =
                new FileStream(
                    fullPath,
                    FileMode.Create))
            {
                await photo.CopyToAsync(stream);
            }

            _context.InsuranceClaimPhotos.Add(
                new InsuranceClaimPhoto
                {
                    InsuranceClaimId =
                        insuranceClaimId,

                    Filename = filename,

                    OriginalFilename =
                        Path.GetFileName(photo.FileName),

                    UploadedDate =
                        DateTime.UtcNow
                });
        }

        await _context.SaveChangesAsync();
    }

    private async Task AssignClaimToTenantAsync(
    InsuranceClaim claim,
    int tenantId)
    {
        var tenant = await _context.Tenant
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x =>
                x.Id == tenantId &&
                x.IsActive);

        if (tenant == null)
            throw new InvalidOperationException("Glazing company not found.");



        var customer = new Customer
        {
            TenantId = tenantId,

            Name = claim.CustomerName,

            Add1 = claim.Address1,
            Add2 = claim.Address2,
            Add3 = claim.Address3,

            Postcode = claim.Postcode,

            Phone1 = claim.Telephone,
            Email = claim.Email
        };

        string temporaryPassword = TemporaryPasswordHelper.Generate();

        temporaryPassword = "temporary";
  
        // Email here
        var hasher = new PasswordHasher<Customer>();

        customer.PasswordHash =
            hasher.HashPassword(
                customer,
                temporaryPassword);

        _context.Customer.Add(customer);

        await _context.SaveChangesAsync();

        int nextContractNumber =
        (await _context.Contract
            .IgnoreQueryFilters()
            .MaxAsync(c => (int?)c.ContractNumber) ?? 0) + 1;

        var customerReference =
            CustomerReferenceHelper.Encode(nextContractNumber);

        var contract = new Contract
        {
            TenantId = tenantId,

            CustomerId = customer.Id,

            ContractNumber = nextContractNumber,

            CustomerReference = customerReference,

            InsurerId = claim.InsurerId,
            InsuranceClaimId = claim.Id,

            IncidentDate = claim.IncidentDate,

            DamageDescription = claim.DamageDescription,

            CauseOfDamage = claim.CauseOfDamage,

            PolicyNumber = claim.PolicyNumber,

            Excess = claim.Excess.ToString("0.00"),
        };

        var update = new ContractUpdate
        {
            TenantId = tenantId,

            Contract = contract,

            DateTime = DateTime.Now,

            Status = ContractStatus.Reported,

            Title = ContractStatusHelper.GetTitle(
                ContractStatus.Reported),

            Description =
                "Your repair has been reported and is being processed.",

            CustomerVisible = true
        };

        _context.Contract.Add(contract);
        _context.ContractUpdates.Add(update);

        claim.AssignedTenantId = tenantId;
        claim.AssignedDate = DateTime.UtcNow;
        claim.Status = InsuranceClaimStatus.Assigned;

        await _context.SaveChangesAsync();
    }



    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        var claim = await _context.InsuranceClaims
            .Include(x => x.Insurer)
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.InsurerId == user.InsurerId.Value);

        if (claim == null)
            return NotFound();

        var requests = await _context.InsuranceClaimRequests
            .Where(x => x.InsuranceClaimId == claim.Id)
            .OrderBy(x => x.RequestedDate)
            .ToListAsync();

        if (requests.Any())
        {
            var tenantIds = requests
                .Select(x => x.TenantId)
                .Distinct()
                .ToList();

            var tenantNames = await _context.Tenant
                .IgnoreQueryFilters()
                .Where(x => tenantIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name);

            ViewBag.JobBoardRequests = requests;
            ViewBag.JobBoardTenantNames = tenantNames;
        }

        if (claim.AssignedTenantId.HasValue)
        {
            ViewBag.AssignedTenantName =
                await _context.Tenant
                    .IgnoreQueryFilters()
                    .Where(x => x.Id == claim.AssignedTenantId.Value)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync();
        }

        var contract = await _context.Contract
            .IgnoreQueryFilters()
            .Where(x => x.InsuranceClaimId == claim.Id)
            .Select(x => new
            {
                x.Id,
                x.ContractCode
            })
            .FirstOrDefaultAsync();

        ViewBag.ContractId = contract?.Id;
        ViewBag.ContractCode = contract?.ContractCode;

        ViewBag.GlazingCompanies =
            new SelectList(
                await _context.Tenant
                    .IgnoreQueryFilters()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Name)
                    .ToListAsync(),
                "Id",
                "Name");

        ViewBag.ClaimUpdates =
            await _context.InsuranceClaimUpdates
            .Where(x => x.InsuranceClaimId == claim.Id)
            .OrderByDescending(x => x.DateTime)
            .ToListAsync();

        return View(claim);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectJobBoardCompany(
    int claimId,
    int requestId)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        var claim = await _context.InsuranceClaims
            .FirstOrDefaultAsync(x =>
                x.Id == claimId &&
                x.InsurerId == user.InsurerId.Value);

        if (claim == null)
            return NotFound();

        if (claim.Status != InsuranceClaimStatus.OnJobBoard)
            return BadRequest("Claim is no longer on the Job Board.");

        var selectedRequest =
            await _context.InsuranceClaimRequests
                .FirstOrDefaultAsync(x =>
                    x.Id == requestId &&
                    x.InsuranceClaimId == claimId);

        if (selectedRequest == null)
            return NotFound();

        await AssignClaimToTenantAsync(
            claim,
            selectedRequest.TenantId);

        var requests = await _context.InsuranceClaimRequests
            .Where(x => x.InsuranceClaimId == claimId)
            .ToListAsync();

        foreach (var request in requests)
        {
            request.Status =
                request.Id == selectedRequest.Id
                    ? InsuranceClaimRequestStatus.Accepted
                    : InsuranceClaimRequestStatus.Rejected;
        }

        await _context.SaveChangesAsync();

        return RedirectToAction(
            nameof(Details),
            new { id = claimId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishToJobBoard(int id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        var claim = await _context.InsuranceClaims
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.InsurerId == user.InsurerId.Value);

        if (claim == null)
            return NotFound();

        if (claim.AssignedTenantId.HasValue)
            return BadRequest("Assigned claims cannot be published to the Job Board.");

        if (claim.Status == InsuranceClaimStatus.OnJobBoard)
            return RedirectToAction(nameof(Details), new { id });

        claim.Status = InsuranceClaimStatus.OnJobBoard;

        AddClaimUpdate(
            claim,
            InsuranceClaimStatus.OnJobBoard,
            "Published to Job Board",
            "The claim was published to the Mart Ops Job Board.");

        await _context.SaveChangesAsync();

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(
    int id,
    int tenantId)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        var claim = await _context.InsuranceClaims
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.InsurerId == user.InsurerId.Value);

        if (claim == null)
            return NotFound();

        if (claim.AssignedTenantId.HasValue)
            return BadRequest("Claim has already been assigned.");

        await AssignClaimToTenantAsync(
            claim,
            tenantId);

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    private void AddClaimUpdate(
    InsuranceClaim claim,
    InsuranceClaimStatus status,
    string title,
    string description)
    {
        var update = new InsuranceClaimUpdate
        {
            InsuranceClaim = claim,

            Status = status,

            DateTime = DateTime.UtcNow,

            Title = title,

            Description = description,

            UserId = _userManager.GetUserId(User)
        };

        _context.InsuranceClaimUpdates.Add(update);
    }


    private async Task PopulateGlazingCompanies()
    {
        ViewBag.GlazingCompanies =
            new SelectList(
                await _context.Tenant
                    .IgnoreQueryFilters()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Name)
                    .ToListAsync(),
                "Id",
                "Name");

    }


    public async Task<IActionResult> Photo(int id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.InsurerId == null)
            return Forbid();

        var photo = await _context.InsuranceClaimPhotos
            .Include(x => x.InsuranceClaim)
            .FirstOrDefaultAsync(x =>
                x.Id == id &&
                x.InsuranceClaim.InsurerId == user.InsurerId.Value);

        if (photo == null)
            return NotFound();

        var imageStoragePath =
            _configuration["ImageStoragePath"]
            ?? throw new InvalidOperationException(
        "ImageStoragePath is not configured.");

        var filePath = Path.Combine(
            imageStoragePath,
            "Insurer",
            photo.InsuranceClaimId.ToString(),
            photo.Filename);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var extension =
            Path.GetExtension(photo.Filename)
                .ToLowerInvariant();

        var contentType = extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(
            filePath,
            contentType);
    }
}