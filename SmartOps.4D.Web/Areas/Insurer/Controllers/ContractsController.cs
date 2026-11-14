using SmartOps.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.StaticFiles;

namespace SmartOps.Web.Areas.Insurer.Controllers
{
    [Area("Insurer")]
    [Authorize(Roles = "Insurer")]
    public class ContractsController : Controller
    {
        private readonly AppDBContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public ContractsController(
            AppDBContext context,
            UserManager<AppUser> userManager,
            IWebHostEnvironment environment)
        {
            _context = context;
            _userManager = userManager;
            _environment = environment;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var userId = _userManager.GetUserId(User);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user?.InsurerId == null)
                return Forbid();

            var contracts = await _context.Contract
                .IgnoreQueryFilters()
                .Include(x => x.Customer)
                .Include(x => x.Tenant)
                .Where(x => x.InsurerId == user.InsurerId)
                .OrderByDescending(x => x.ContractNumber)
                .ToRecordPageAsync(page, ViewData);

            var model = new List<InsurerContractListItemViewModel>();

            foreach (var contract in contracts)
            {
                var unread = await _context.InsurerMessages
                    .IgnoreQueryFilters()
                    .CountAsync(x =>
                        x.ContractId == contract.Id &&
                        x.InsurerId == user.InsurerId &&
                        x.Sender == InsurerMessageSender.Company &&
                        !x.ReadByInsurer);

                model.Add(new InsurerContractListItemViewModel
                {
                    Contract = contract,
                    UnreadMessages = unread
                });
            }

            return View(model);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user?.InsurerId == null)
                return Forbid();

            var contract = await _context.Contract
                .IgnoreQueryFilters()
                .Include(x => x.Customer)
                .Include(x => x.Insurer)
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    x.InsurerId == user.InsurerId);

            if (contract == null)
                return NotFound();

            await _context.AuditBulkAsync("InsurerMessages", contract.TenantId, "Updated", () => _context.InsurerMessages // mark messages as read by insurer
                .IgnoreQueryFilters()
                .Where(x =>
                    x.ContractId == contract.Id &&
                    x.InsurerId == user.InsurerId &&
                    x.Sender == InsurerMessageSender.Company &&
                    !x.ReadByInsurer)
                .ExecuteUpdateAsync(x =>
                    x.SetProperty(
                        m => m.ReadByInsurer,
                        true)));

            var updates = await _context.ContractUpdates
                .IgnoreQueryFilters()
                .Where(x => x.ContractId == contract.Id &&
                            x.TenantId == contract.TenantId &&
                            x.CustomerVisible)
                .OrderByDescending(x => x.DateTime)
                .ToListAsync();

            var messages = await _context.InsurerMessages
                .IgnoreQueryFilters()
                .Include(x => x.SentByUser)
                .Where(x =>
                    x.ContractId == contract.Id &&
                    x.InsurerId == user.InsurerId)
                .OrderBy(x => x.Created)
                .ToListAsync();

            // Use the same visibility rules as the customer repair progress endpoint.
            // Both contract code and tenant are checked to avoid cross-tenant matches.
            var photos = await _context.Images
                .IgnoreQueryFilters()
                .Where(x => x.ContractCode == contract.ContractCode &&
                            x.TenantId == contract.TenantId &&
                            x.CustomerVisible)
                .OrderBy(x => x.DateTime)
                .ToListAsync();

            ViewBag.Estimates = await _context.Estimates.IgnoreQueryFilters().AsNoTracking()
                .Include(e => e.Lines.Where(l => l.TenantId == contract.TenantId))
                .Where(e => e.ContractId == contract.Id && e.TenantId == contract.TenantId)
                .OrderByDescending(e => e.CreatedUtc).ThenByDescending(e => e.Id).ToListAsync();

            ViewBag.ContractPhotos = photos;
            ViewBag.ContractUpdates = updates;
            ViewBag.InsurerMessages = messages;

            return View(contract);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AuthorizeEstimate(int contractId, int estimateId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.InsurerId == null) return Forbid();
            var contract = await _context.Contract.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == contractId && c.InsurerId == user.InsurerId);
            if (contract == null) return NotFound();
            if (contract.IsCancelled) return BadRequest("This contract is cancelled.");
            // Insurer accounts normally have no company tenant. Reject a conflicting company scope.
            if (_context.CurrentTenantId.HasValue && _context.CurrentTenantId != contract.TenantId) return Forbid();
            var estimate = await _context.Estimates.IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.Id == estimateId && e.ContractId == contract.Id && e.TenantId == contract.TenantId);
            if (estimate == null) return NotFound();
            try
            {
                await SmartOps.Web.Services.EstimateAuthorization.AuthorizeAsync(
                    _context, contract, estimate, user.Email ?? user.UserName ?? user.Id);
            }
            catch (DbUpdateConcurrencyException) { return Conflict("The estimate changed. Reload the contract and try again."); }
            return Redirect((Url.Action(nameof(Details), new { id = contractId }) ?? "/") + "#estimates");
        }

        // Stream insurer-visible media through an authorised route. Do not link the
        // insurer UI directly to the publicly served /images directory.
        [HttpGet]
        public async Task<IActionResult> Photo(int id, int photoId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.InsurerId == null)
                return Forbid();

            var contract = await _context.Contract
                .IgnoreQueryFilters()
                .Where(x => x.Id == id && x.InsurerId == user.InsurerId)
                .Select(x => new { x.ContractCode, x.TenantId })
                .FirstOrDefaultAsync();

            if (contract == null)
                return NotFound();

            var photo = await _context.Images
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == photoId &&
                    x.TenantId == contract.TenantId &&
                    x.ContractCode == contract.ContractCode &&
                    x.CustomerVisible);

            if (photo == null)
                return NotFound();

            // The database stores the filename, not a trusted relative path.
            var safeName = Path.GetFileName(photo.Filename);
            if (string.IsNullOrWhiteSpace(safeName) || safeName != photo.Filename)
                return NotFound();

            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrEmpty(webRoot))
                return NotFound();

            var filePath = Path.Combine(webRoot, "images", safeName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(safeName, out var contentType) ||
                !(contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                  contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
                return NotFound();

            return PhysicalFile(filePath, contentType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyToCompany(
            int contractId,
            string insurerMessage)
        {
            if (string.IsNullOrWhiteSpace(insurerMessage))
            {
                return Redirect((Url.Action(nameof(Details), new { id = contractId }) ?? "/") + "#messages");
            }

            var userId = _userManager.GetUserId(User);

            var user = await _userManager.Users
                .FirstOrDefaultAsync(x => x.Id == userId);

            if (user?.InsurerId == null)
                return Forbid();

            var contract = await _context.Contract
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x =>
                    x.Id == contractId &&
                    x.InsurerId == user.InsurerId);

            if (contract == null)
                return NotFound();

            var message = new InsurerMessage
            {
                TenantId = contract.TenantId,

                ContractId = contract.Id,
                InsurerId = user.InsurerId.Value,

                Message = insurerMessage.Trim(),

                Sender = InsurerMessageSender.Insurer,

                SentByUserId = user.Id,

                Created = DateTime.UtcNow,

                ReadByInsurer = true,
                ReadByCompany = false
            };

            _context.InsurerMessages.Add(message);

            await _context.SaveChangesAsync();

            return Redirect((Url.Action(nameof(Details), new { id = contractId }) ?? "/") + "#messages");
        }
    }
}