// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace SmartOps.Web.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly IUserStore<AppUser> _userStore;
        private readonly IUserEmailStore<AppUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;
        private readonly AppDBContext _context;

        public RegisterModel(
            UserManager<AppUser> userManager,
            IUserStore<AppUser> userStore,
            SignInManager<AppUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            AppDBContext context,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _configuration = configuration;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Display(Name = "Create demonstration data")]
            public bool CreateDemoData { get; set; } = true;

            [Required]
            [Display(Name = "Company Name")]
            public string TenantName { get; set; }

            [Required]
            [StringLength(20)]
            [Display(Name = "Company Code")]
            public string TenantCode { get; set; }

            [Required]
            [Display(Name = "First Name")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(
                100,
                ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.",
                MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare(
                "Password",
                ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<JsonResult> OnGetCheckTenantCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new JsonResult(new { exists = false });

            var exists = await _context.Tenant
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Code == code);

            return new JsonResult(new { exists });
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                .ToList();

            if (!ModelState.IsValid)
                return Page();

            // Reject duplicates before creating the tenant or generating any sample data.
            Input.Email = Input.Email.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(Input.Email);
            var emailExists = await _userManager.Users.IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail);
            if (emailExists)
            {
                ModelState.AddModelError("Input.Email", "This email address is already in use.");
                return Page();
            }

            var tenantName = Input.TenantName.Trim();

            tenantName = CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(tenantName.ToLower());

            if (string.IsNullOrWhiteSpace(tenantName))
            {
                ModelState.AddModelError(
                    "Input.TenantName",
                    "Please enter a company name.");

                return Page();
            }

            // -------------------------------------------------
            // Prepare tenant code
            // -------------------------------------------------

            var tenantCode = Input.TenantCode
                .Trim()
                .ToUpperInvariant();

            // -------------------------------------------------
            // Check tenant doesn't already exist
            // -------------------------------------------------

            var tenantExists = await _context.Tenant
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Code == tenantCode);

            if (tenantExists)
            {
                ModelState.AddModelError(
                    "Input.TenantCode",
                    "That company code is already in use.");

                return Page();
            }

            var autoApprove = _configuration.GetValue("Registration:AutoApprove", true);
            var requireEmailConfirmation = _configuration.GetValue<bool>("Registration:RequireEmailConfirmation");

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // -------------------------------------------------
                // Create tenant
                // -------------------------------------------------

                var tenant = new Tenant
                {
                    Name = tenantName,
                    Code = tenantCode,
                    IsActive = autoApprove
                };

                _context.Tenant.Add(tenant);

                await _context.SaveChangesAsync();

                // -------------------------------------------------
                // Create tenant administrator
                // -------------------------------------------------

                var user = CreateUser();

                user.FirstName = Input.FirstName.Trim();
                user.LastName = Input.LastName.Trim();
                user.TenantId = tenant.Id;
                user.IsApproved = autoApprove;
                user.EmailConfirmed = !requireEmailConfirmation;
                user.ApprovedAt = autoApprove ? DateTime.UtcNow : null;

                await _userStore.SetUserNameAsync(
                    user,
                    tenantCode,
                    CancellationToken.None);

                await _emailStore.SetEmailAsync(
                    user,
                    Input.Email,
                    CancellationToken.None);

                var result = await _userManager.CreateAsync(
                    user,
                    Input.Password);

                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            error.Description);
                    }

                    await transaction.RollbackAsync();
                    return Page();
                }

                // -------------------------------------------------
                // Make first user an Admin
                // -------------------------------------------------

                var roleResult =
                    await _userManager.AddToRoleAsync(
                        user,
                        "Admin");

                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            error.Description);
                    }

                    await transaction.RollbackAsync();
                    return Page();
                }

                _logger.LogInformation(
                    "User created a new tenant account.");

                // -------------------------------------------------
                // Create test/demo data if requested
                // -------------------------------------------------

                if (Input.CreateDemoData)
                {
                    await DbInitializer.AddTestDataAsync(
                        _context,
                        tenant.Id);
                }
                else
                {
                    await DbInitializer.AddSmallTestDataAsync(
                        _context,
                        tenant.Id);
                }

                // Persist the tenant and administrator before signing in.
                await transaction.CommitAsync();

                if (autoApprove && !requireEmailConfirmation)
                {
                    _logger.LogInformation("Registration auto-approved for {UserId}", user.Id);
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Content("~/Jobs"));
                }

                try
                {
                    if (!autoApprove)
                    {
                        var reviewUrl = Url.Action("Review", "RegistrationApproval",
                            new { area = "", userId = user.Id }, Request.Scheme);
                        await _emailSender.SendEmailAsync("craig.griffiths2007@gmail.com",
                            $"Mart Ops registration awaiting approval - {tenant.Name}",
                            $"<p>A registration for {HtmlEncoder.Default.Encode(tenant.Name)} is awaiting approval.</p>" +
                            $"<p><a href=\"{HtmlEncoder.Default.Encode(reviewUrl!)}\">Review registration</a></p>");
                    }
                    else
                    {
                        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                        var confirmationUrl = Url.Page("/Account/ConfirmEmail", null,
                            new { area = "Identity", userId = user.Id, code }, Request.Scheme);
                        await _emailSender.SendEmailAsync(user.Email!, "Confirm your Mart Ops account",
                            $"<p>Please <a href=\"{HtmlEncoder.Default.Encode(confirmationUrl!)}\">confirm your email address</a> to sign in.</p>");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to send registration notification for {UserId}", user.Id);
                }

                return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private AppUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<AppUser>();
            }
            catch
            {
                throw new InvalidOperationException(
                    $"Can't create an instance of '{nameof(AppUser)}'. " +
                    $"Ensure that '{nameof(AppUser)}' is not an abstract class and has a parameterless constructor, " +
                    "or alternatively override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<AppUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(
                    "The default UI requires a user store with email support.");
            }

            return (IUserEmailStore<AppUser>)_userStore;
        }
    }
}
