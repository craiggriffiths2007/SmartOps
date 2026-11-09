using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;

namespace SmartOps.Web.Controllers
{

    [Authorize(Roles = "Admin,Diary,SuperAdmin")]
    public class JobBoardController : Controller
    {
        private readonly AppDBContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public JobBoardController(
            AppDBContext context,
            UserManager<AppUser> userManager,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            bool isSuperAdmin = User.IsInRole("SuperAdmin");

            int? tenantId = _context.CurrentTenantId;

            if (!isSuperAdmin && !tenantId.HasValue)
                return Forbid();

            var claims = await _context.InsuranceClaims
                .Where(x =>
                    x.Status == InsuranceClaimStatus.OnJobBoard)
                .OrderByDescending(x => x.Created)
                .ThenByDescending(x => x.Id)
                .Select(x => new JobBoardItemViewModel
                {
                    Id = x.Id,

                    ClaimReference = x.ClaimReference,

                    Postcode = x.Postcode,

                    IncidentDate = x.IncidentDate,

                    CauseOfDamage = x.CauseOfDamage,

                    DamageDescription = x.DamageDescription,

                    Requested = !isSuperAdmin &&
                        _context.InsuranceClaimRequests
                            .Any(r =>
                                r.InsuranceClaimId == x.Id &&
                                r.TenantId == tenantId!.Value)
                })
                .ToRecordPageAsync(page, ViewData);

            return View(claims);
        }

        public async Task<IActionResult> MyRequests(int page = 1)
        {
            var tenantId = _context.CurrentTenantId;

            if (!tenantId.HasValue)
                return Forbid();

            var requests = await _context.InsuranceClaimRequests
                .Where(x => x.TenantId == tenantId.Value)
                .OrderByDescending(x => x.RequestedDate)
                .ThenByDescending(x => x.Id)
                .Select(x => new JobRequestListItemViewModel
                {
                    RequestId = x.Id,

                    ClaimId = x.InsuranceClaimId,

                    Postcode = x.InsuranceClaim.Postcode,

                    IncidentDate = x.InsuranceClaim.IncidentDate,

                    CauseOfDamage = x.InsuranceClaim.CauseOfDamage,

                    RequestedDate = x.RequestedDate,

                    Status = x.Status,

                    ContractId = _context.Contract
                        .IgnoreQueryFilters()
                        .Where(c =>
                            c.InsuranceClaimId == x.InsuranceClaimId &&
                            c.TenantId == tenantId.Value)
                        .Select(c => (int?)c.Id)
                        .FirstOrDefault(),

                    ContractCode = _context.Contract
                        .IgnoreQueryFilters()
                        .Where(c =>
                            c.InsuranceClaimId == x.InsuranceClaimId &&
                            c.TenantId == tenantId.Value)
                        .Select(c => c.ContractCode)
                        .FirstOrDefault() ?? ""
                })
                .ToRecordPageAsync(page, ViewData);

            return View(requests);
        }

        public async Task<IActionResult> RequestDetails(int id)
        {
            var tenantId = _context.CurrentTenantId;

            if (!tenantId.HasValue)
                return Forbid();

            var request = await _context.InsuranceClaimRequests
                .Include(x => x.InsuranceClaim)
                    .ThenInclude(x => x.Insurer)
                .Include(x => x.InsuranceClaim)
                    .ThenInclude(x => x.Photos)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.TenantId == tenantId.Value);

            if (request == null)
                return NotFound();

            var contract = await _context.Contract
                .IgnoreQueryFilters()
                .Where(x =>
                    x.InsuranceClaimId == request.InsuranceClaimId &&
                    x.TenantId == tenantId.Value)
                .Select(x => new
                {
                    x.Id,
                    x.ContractCode
                })
                .FirstOrDefaultAsync();

            ViewBag.ContractId = contract?.Id;
            ViewBag.ContractCode = contract?.ContractCode;

            return View(request);
        }

        public async Task<IActionResult> Details(int id)
        {
            bool isSuperAdmin = User.IsInRole("SuperAdmin");

            int? tenantId = _context.CurrentTenantId;

            if (!isSuperAdmin && !tenantId.HasValue)
                return Forbid();

            var claim = await _context.InsuranceClaims
                .Include(x => x.Photos)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.Status == InsuranceClaimStatus.OnJobBoard);

            if (claim == null)
                return NotFound();

            ViewBag.Requested = false;
            ViewBag.RequestedDate = null;

            if (!isSuperAdmin)
            {
                var request = await _context.InsuranceClaimRequests
                    .FirstOrDefaultAsync(x =>
                        x.InsuranceClaimId == id &&
                        x.TenantId == tenantId!.Value);

                if (request != null)
                {
                    ViewBag.Requested = true;
                    ViewBag.RequestedDate = request.RequestedDate;
                }
            }

            if (isSuperAdmin)
            {
                ViewBag.Requests = await _context.InsuranceClaimRequests
                    .Where(x => x.InsuranceClaimId == id)
                    .OrderBy(x => x.RequestedDate)
                    .ToListAsync();
            }

            return View(claim);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Diary")]
        public async Task<IActionResult> RequestJob(int id)
        {
            var tenantId = _context.CurrentTenantId;

            if (!tenantId.HasValue)
                return Forbid();

            var claim = await _context.InsuranceClaims
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.Status == InsuranceClaimStatus.OnJobBoard);

            if (claim == null)
                return NotFound();

            bool alreadyRequested =
                await _context.InsuranceClaimRequests
                    .AnyAsync(x =>
                        x.InsuranceClaimId == id &&
                        x.TenantId == tenantId.Value);

            if (!alreadyRequested)
            {
                var request = new InsuranceClaimRequest
                {
                    InsuranceClaimId = id,
                    TenantId = tenantId.Value,
                    RequestedDate = DateTime.UtcNow,
                    Status = InsuranceClaimRequestStatus.Pending
                };

                _context.InsuranceClaimRequests.Add(request);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ClaimPhoto(int id)
        {
            var tenantId = _context.CurrentTenantId;

            if (!tenantId.HasValue)
                return Forbid();

            var photo = await _context.InsuranceClaimPhotos
                .Include(x => x.InsuranceClaim)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (
                        x.InsuranceClaim.Status == InsuranceClaimStatus.OnJobBoard ||
                        x.InsuranceClaim.AssignedTenantId == tenantId.Value
                    ));

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
}
