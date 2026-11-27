using SmartOps.Web.Bridge;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using SmartOps.Web.Helpers;
using SmartOps.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SkiaSharp;

namespace SmartOps.Web.Controllers
{
    [Authorize(Roles = StaffRoles.ManageUsers)]
    public class UsersController : Controller
    {
        private readonly AccountService _accounts;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private readonly AppDBContext _context;
        private readonly ITenantContext _tenantContext;
        private readonly IEmailSender _emailSender;
        private readonly EmailLoginLinkService _loginLinks;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
                UserManager<AppUser> userManager,
                RoleManager<IdentityRole> roleManager,
                AppDBContext context,
                ITenantContext tenantContext,
                IEmailSender emailSender,
                ILogger<UsersController> logger,
                EmailLoginLinkService loginLinks,
                AccountService accounts)
        {
            _accounts = accounts;
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _tenantContext = tenantContext;
            _emailSender = emailSender;
            _logger = logger;
            _loginLinks = loginLinks;
        }

        // GET: Users
        public async Task<IActionResult> Index(int page = 1)
        {
            IQueryable<AppUser> query = _userManager.Users.Where(u => u.Id.StartsWith("4d-account-"));

            if (!User.IsInRole("SuperAdmin"))
            {
                var currentTenantId = _tenantContext.TenantId;

                if (!currentTenantId.HasValue)
                    return Forbid();

                query = query.Where(u =>
                    u.TenantId == currentTenantId.Value);
            }

            var protectedRoles = User.IsInRole("SuperAdmin") || User.IsInRole(StaffRoles.Admin)
                ? new[] { "SuperAdmin", "Insurer" }
                : new[] { "Admin", "SuperAdmin", "Insurer" };
            var protectedIds = from membership in _context.UserRoles
                               join role in _context.Roles on membership.RoleId equals role.Id
                               where protectedRoles.Contains(role.Name!)
                               select membership.UserId;
            query = query.Where(u => !protectedIds.Contains(u.Id));

            var users = await query
                .OrderBy(u => u.UserName)
                .ThenBy(u => u.Id)
                .ToRecordPageAsync(page, ViewData);

            var tenants = await _context.Tenant
                .ToDictionaryAsync(t => t.Id, t => t.Name);

            var userList = new List<UserRolesViewModel>();

            foreach (var user in users)
            {
                string tenantName = "SUPERADMIN";

                if (user.TenantId.HasValue)
                {
                    tenantName = tenants.TryGetValue(
                        user.TenantId.Value,
                        out var name)
                            ? name
                            : "Unknown";
                }

                userList.Add(new UserRolesViewModel
                {
                    UserId = user.Id,
                    BranchCode = user.BranchCode,
                    LockBranch = user.LockBranch,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,

                    TenantId = user.TenantId,
                    Tenant = tenantName,

                    Roles = await _userManager.GetRolesAsync(user),
                    ProfilePicture = user.ProfilePicture
                });
            }

            return View(userList);
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);

            if (user == null || !await CanManageUser(user))
                return NotFound();

            // Tenant security
            if (!User.IsInRole("SuperAdmin"))
            {
                if (!_tenantContext.TenantId.HasValue)
                    return Forbid();

                if (user.TenantId != _tenantContext.TenantId.Value)
                    return NotFound();
            }

            ViewData["RegistrationUser"] = user;
            var model = new UserRolesViewModel
            {
                UserId = user.Id,
                BranchCode = user.BranchCode,
                LockBranch = user.LockBranch,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,

                TenantId = user.TenantId,

                Roles = await _userManager.GetRolesAsync(user),

                ProfilePicture = user.ProfilePicture
            };

            return View(model);
        }
        private async Task<List<SelectListItem>> GetTenants()
        {
            return await _context.Tenant
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetBranches() => await _context.Branches
            .Where(b => b.Code != null && b.Code != "")
            .OrderBy(b => b.Name)
            .Select(b => new SelectListItem { Value = b.Code!.Trim(), Text = string.IsNullOrWhiteSpace(b.Name) ? "Unnamed branch" : b.Name.Trim() })
            .ToListAsync();

        private async Task ValidateBranch(UserFormViewModel model)
        {
            model.BranchCode = string.IsNullOrWhiteSpace(model.BranchCode) ? null : model.BranchCode.Trim();
            if (model.BranchCode != null && !await _context.Branches.AnyAsync(b => b.Code != null && b.Code.Trim() == model.BranchCode))
                ModelState.AddModelError(nameof(model.BranchCode), "Choose a branch from your company or All.");
        }

        // GET: Users/Create
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel
            {
                Roles = await GetAllRoles(),
                Tenants = await GetTenants(),
                Branches = await GetBranches(),
                Password = TemporaryUserPassword.Generate()
            };
            foreach (var role in model.Roles)
                role.Selected = StaffRoles.Defaults.Contains(role.RoleName);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult GeneratePassword() => Json(new { password = TemporaryUserPassword.Generate() });

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model, bool emailLogin = false)
        {
            if (!User.IsInRole("SuperAdmin") && !_tenantContext.TenantId.HasValue)
                return Forbid();

            await ValidateBranch(model);
            var selectedRoles = model.Roles.Where(r => r.Selected).Select(r => r.RoleName).ToHashSet();
            var allowedRoles = await GetAllRoles();
            if (selectedRoles.Except(allowedRoles.Select(r => r.RoleName)).Any())
                ModelState.AddModelError("", "Please select valid roles.");

            if (ModelState.IsValid)
            {
                var user = new AppUser
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    BranchCode = model.BranchCode,
                    LockBranch = model.LockBranch,
                    EmailConfirmed = true,
                    IsApproved = true,
                    ApprovedAt = DateTime.UtcNow,
                    TenantId = User.IsInRole("SuperAdmin") ? model.TenantId : _tenantContext.TenantId
                };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    var rolesResult = await _userManager.AddToRolesAsync(user, selectedRoles);
                    if (!rolesResult.Succeeded)
                    {
                        TempData["UserLoginWarning"] = "User created, but the roles could not be assigned. Please edit the user to check their roles.";
                        return RedirectToAction(nameof(Details), new { id = user.Id });
                    }
                    try
                    {
                        var savedId = await _accounts.SaveUser(user, selectedRoles, model.Password, create: true);
                        _context.Entry(user).State = EntityState.Detached;
                        user.Id = savedId;
                    }
                    catch (Exception error) when (error is AccountValidationException or HttpRequestException)
                    {
                        ModelState.AddModelError("", error.Message);
                        foreach (var role in allowedRoles) role.Selected = selectedRoles.Contains(role.RoleName);
                        model.Roles = allowedRoles;
                        model.Tenants = await GetTenants();
                        model.Branches = await GetBranches();
                        return View(model);
                    }
                    if (emailLogin)
                        await EmailLoginDetails(user, model.Password);
                    else
                        TempData["UserLoginSuccess"] = "User created. The password can be changed after logging in.";
                    return RedirectToAction(nameof(Details), new { id = user.Id });
                }
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }
            foreach (var role in allowedRoles)
                role.Selected = selectedRoles.Contains(role.RoleName);
            model.Roles = allowedRoles;
            model.Tenants = await GetTenants();
            model.Branches = await GetBranches();
            return View(model);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendConfirmation(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await CanManageUser(user))
                return NotFound();

            if (user.EmailConfirmed)
            {
                TempData["UserLoginWarning"] = "This user's email address has already been confirmed.";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var callbackUrl = Url.Page("/Account/ConfirmEmail", pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code }, protocol: Request.Scheme);
                await _emailSender.SendEmailAsync(user.Email!, "Confirm your Mart Ops account",
                    $"<h2>Confirm your Mart Ops account</h2><p>Please " +
                    $"<a href=\"{HtmlEncoder.Default.Encode(callbackUrl!)}\">confirm your email address</a>.</p>");
                TempData["UserLoginSuccess"] = $"Registration confirmation emailed to {user.Email}.";
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not resend registration confirmation for user {UserId}", user.Id);
                TempData["UserLoginWarning"] = "The confirmation email could not be sent. Please check email settings and try again.";
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task EmailLoginDetails(AppUser user, string password)
        {
            var loginToken = _loginLinks.Create(user.Id, user.UserName ?? "", password, user.SecurityStamp);
            var loginUrl = Url.Page("/Account/Login", pageHandler: null,
                values: new { area = "Identity", loginToken }, protocol: Request.Scheme);
            var encode = HtmlEncoder.Default;
            var name = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
            var body = $"""
                <p>Hello {encode.Encode(string.IsNullOrWhiteSpace(name) ? user.UserName ?? "" : name)},</p>
                <p>Your Mart Ops login details are:</p>
                <p><strong>Username:</strong> {encode.Encode(user.UserName ?? "")}<br />
                <strong>Email:</strong> {encode.Encode(user.Email ?? "")}<br />
                <strong>Password:</strong> {encode.Encode(password)}</p>
                <p><a href="{encode.Encode(loginUrl ?? "")}">Log in to Mart Ops</a></p>
                <p>This link fills in your login details once, within 30 minutes. You can also enter the details above manually.</p>
                <p>Please change your password after logging in. You can also add your profile photo in your account settings.</p>
                <p>Kind regards,<br />Mart Ops</p>
                """;
            try
            {
                await _emailSender.SendEmailAsync(user.Email!, "Your Mart Ops login details", body);
                TempData["UserLoginSuccess"] = $"Login details saved and emailed to {user.Email}.";
            }
            catch (Exception exception)
            {
                _loginLinks.Revoke(loginToken);
                _logger.LogWarning(exception, "Could not email login details for user {UserId}", user.Id);
                TempData["UserLoginWarning"] = "The user was saved, but the email could not be sent. Please check email settings, then generate and email a new password from Edit User.";
            }
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);

            if (user == null || !await CanManageUser(user))
                return NotFound();

            if (!User.IsInRole("SuperAdmin") &&
                (!_tenantContext.TenantId.HasValue || user.TenantId != _tenantContext.TenantId))
                return NotFound();

            var roles = await GetAllRoles();
            var userRoles = await _userManager.GetRolesAsync(user);

            foreach (var role in roles)
            {
                role.Selected = userRoles.Contains(role.RoleName);
            }

            ViewData["RegistrationUser"] = user;
            var model = new EditUserViewModel
            {
                Id = user.Id,
                BranchCode = user.BranchCode,
                LockBranch = user.LockBranch,
                UserName = user.UserName ?? "",
                Email = user.Email ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                TenantId = user.TenantId,
                Tenants = await GetTenants(),
                Branches = await GetBranches(),
                ExistingPhoto = user.ProfilePicture,
                Roles = roles
            };

            return View(model);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
    string id,
    EditUserViewModel model,
    bool emailLogin = false)
        {
            if (id != model.Id)
                return NotFound();

            var targetUser = await _userManager.FindByIdAsync(id);
            if (targetUser == null || !await CanManageUser(targetUser))
                return NotFound();
            if (!User.IsInRole("SuperAdmin") &&
                (!_tenantContext.TenantId.HasValue || targetUser.TenantId != _tenantContext.TenantId))
                return NotFound();
            await ValidateBranch(model);
            var selectedRoles = model.Roles.Where(r => r.Selected).Select(r => r.RoleName).ToHashSet();
            var allowedRoles = await GetAllRoles();
            if (selectedRoles.Except(allowedRoles.Select(r => r.RoleName)).Any())
                ModelState.AddModelError("", "Please select valid roles. Only an Admin can assign Admin.");
            if (emailLogin && string.IsNullOrWhiteSpace(model.NewPassword))
                ModelState.AddModelError(nameof(model.NewPassword), "Generate or enter a new password before emailing login details.");

            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(id);

                if (user == null)
                    return NotFound();

                user.UserName = model.UserName;
                user.Email = model.Email;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.BranchCode = model.BranchCode;
                user.LockBranch = model.LockBranch;

                if (User.IsInRole("SuperAdmin"))
                {
                    user.TenantId = model.TenantId;
                }
                else
                {
                    user.TenantId = _tenantContext.TenantId;
                }

                if(model.Photo != null && model.Photo.Length > 0)
{
                    user.ProfilePicture = await ResizeProfilePicture(model.Photo);
                    user.ProfilePictureUpdated = DateTime.UtcNow;
                }

                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    // Reset password if a new one was provided
                    if (!string.IsNullOrWhiteSpace(model.NewPassword))
                    {
                        var token =
                            await _userManager.GeneratePasswordResetTokenAsync(user);

                        var passwordResult =
                            await _userManager.ResetPasswordAsync(
                                user,
                                token,
                                model.NewPassword);

                        if (!passwordResult.Succeeded)
                        {
                            foreach (var error in passwordResult.Errors)
                            {
                                ModelState.AddModelError(
                                    "",
                                    error.Description);
                            }

                            await PopulateEditLists(model, user);
                            return View(model);
                        }
                    }

                    var currentRoles =
                        await _userManager.GetRolesAsync(user);

                    var addResult = await _userManager.AddToRolesAsync(user, selectedRoles.Except(currentRoles));
                    if (!addResult.Succeeded)
                    {
                        foreach (var error in addResult.Errors) ModelState.AddModelError("", error.Description);
                        await PopulateEditLists(model, user);
                        return View(model);
                    }
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles.Except(selectedRoles));
                    if (!removeResult.Succeeded)
                    {
                        foreach (var error in removeResult.Errors) ModelState.AddModelError("", error.Description);
                        await PopulateEditLists(model, user);
                        return View(model);
                    }
                    // Refresh existing login cookies after changing permissions.
                    if (!currentRoles.ToHashSet().SetEquals(selectedRoles))
                        await _userManager.UpdateSecurityStampAsync(user);

                    try
                    {
                        await _accounts.SaveUser(user, selectedRoles, model.NewPassword);
                    }
                    catch (Exception error) when (error is AccountValidationException or HttpRequestException)
                    {
                        ModelState.AddModelError("", error.Message);
                        await PopulateEditLists(model, user);
                        return View(model);
                    }
                    if (_accounts.IsCurrentUser(user.Id) &&
                        (!string.IsNullOrWhiteSpace(model.NewPassword) || !currentRoles.ToHashSet().SetEquals(selectedRoles) ||
                         !string.Equals(user.UserName, User.Identity?.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                        return LocalRedirect("/Account/Login?passwordChanged=true");
                    }
                    if (emailLogin)
                        await EmailLoginDetails(user, model.NewPassword!);
                    return RedirectToAction(nameof(Details), new { id = user.Id });
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(
                        "",
                        error.Description);
                }
            }

            var existingUser =
                await _userManager.FindByIdAsync(id);

            if (existingUser == null)
                return NotFound();

            await PopulateEditLists(model, existingUser);

            return View(model);
        }

        private async Task PopulateEditLists(
                    EditUserViewModel model,
                    AppUser user)
        {
            var roles = await GetAllRoles();
            var userRoles = await _userManager.GetRolesAsync(user);

            foreach (var role in roles)
            {
                var postedRole = model.Roles?
                    .FirstOrDefault(r => r.RoleName == role.RoleName);

                role.Selected = postedRole?.Selected
                    ?? userRoles.Contains(role.RoleName);
            }

            ViewData["RegistrationUser"] = user;
            model.Roles = roles;
            model.Tenants = await GetTenants();
            model.Branches = await GetBranches();
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await CanManageUser(user))
                return NotFound();

            var model = new UserRolesViewModel
            {
                UserId = user.Id,
                BranchCode = user.BranchCode,
                LockBranch = user.LockBranch,
                UserName = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Roles = await _userManager.GetRolesAsync(user),
                //ProfilePicture = user.ProfilePicture
            };
            return View(model);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await CanManageUser(user))
                return NotFound();
            try
            {
                await _accounts.DeleteUser(id);
            }
            catch (Exception error) when (error is AccountValidationException or HttpRequestException)
            {
                TempData["UserLoginWarning"] = error.Message;
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["UserLoginWarning"] = "The user could not be deleted. Please try again.";
                return RedirectToAction(nameof(Details), new { id });
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<ManageUserRolesViewModel>> GetAllRoles()
        {
            var names = User.IsInRole("SuperAdmin") || User.IsInRole(StaffRoles.Admin) ? StaffRoles.All : StaffRoles.Defaults;
            var roles = await _roleManager.Roles.Where(r => names.Contains(r.Name!)).ToListAsync();
            return roles.OrderBy(r => Array.IndexOf(StaffRoles.All, r.Name!))
                .Select(r => new ManageUserRolesViewModel
                {
                    RoleId = r.Id,
                    RoleName = r.Name ?? "",
                    Selected = false
                }).ToList();
        }

        private async Task<bool> CanManageUser(AppUser user)
        {
            if (!User.IsInRole("SuperAdmin") &&
                (!_tenantContext.TenantId.HasValue || user.TenantId != _tenantContext.TenantId))
                return false;
            var roles = await _userManager.GetRolesAsync(user);
            return !roles.Contains("SuperAdmin") && !roles.Contains("Insurer")
                && (User.IsInRole("SuperAdmin") || User.IsInRole(StaffRoles.Admin) || !roles.Contains(StaffRoles.Admin));
        }

        private async Task<byte[]> ResizeProfilePicture(IFormFile photo)
        {
            using var input = photo.OpenReadStream();
            using var memoryStream = new MemoryStream();

            await input.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            using var original = SKBitmap.Decode(memoryStream);

            if (original == null)
                throw new InvalidOperationException("Unable to read profile picture.");

            const int maxSize = 400;

            int newWidth = original.Width;
            int newHeight = original.Height;

            if (original.Width > maxSize || original.Height > maxSize)
            {
                double scale = Math.Min(
                    (double)maxSize / original.Width,
                    (double)maxSize / original.Height);

                newWidth = (int)(original.Width * scale);
                newHeight = (int)(original.Height * scale);
            }

            using var resized = original.Resize(
                new SKSizeI(newWidth, newHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

            if (resized == null)
                throw new InvalidOperationException("Unable to resize profile picture.");

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 85);

            return data.ToArray();
        }
    }

}
