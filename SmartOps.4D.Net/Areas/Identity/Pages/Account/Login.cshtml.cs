// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using SmartOps.Web.Data;
using SmartOps.Web.Services;

namespace SmartOps.Web.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<AppUser> _signInManager;
        private readonly UserManager<AppUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly EmailLoginLinkService _loginLinks;
        public string LoginToken { get; private set; }

        public LoginModel(SignInManager<AppUser> signInManager, UserManager<AppUser> userManager, ILogger<LoginModel> logger, EmailLoginLinkService loginLinks)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _loginLinks = loginLinks;
        }

        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            [Required]
            [Display(Name = "Email or Username")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Display(Name = "Stay signed in")]
            public bool RememberMe { get; set; } = true;
        }

        public async Task OnGetAsync(
                    string returnUrl = null,
                    string username = null,
                    string loginToken = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(
                    string.Empty,
                    ErrorMessage);
            }

            // Loading the email link does not consume it; redemption happens in the browser via POST.
            Response.Headers.CacheControl = "no-store";
            Response.Headers["Referrer-Policy"] = "no-referrer";
            LoginToken = loginToken?.Length == 43 ? loginToken : null;
            returnUrl ??= Url.Content("~/");

            await HttpContext.SignOutAsync(
                IdentityConstants.ExternalScheme);

            ExternalLogins =
                (await _signInManager
                    .GetExternalAuthenticationSchemesAsync())
                .ToList();

            ReturnUrl = returnUrl;

            Input = new InputModel
            {
                Email = username ?? ""
            };
        }

        public async Task<IActionResult> OnPostPrefillAsync(string loginToken)
        {
            Response.Headers.CacheControl = "no-store";
            var details = _loginLinks.Redeem(loginToken);
            if (details == null) return BadRequest();
            var user = await _userManager.FindByIdAsync(details.UserId);
            if (user == null || !user.IsApproved ||
                user.UserName != details.Username || user.SecurityStamp != details.SecurityStamp ||
                !await _userManager.CheckPasswordAsync(user, details.Password))
                return BadRequest();
            return new JsonResult(new { username = details.Username, password = details.Password });
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Resolve the username: if input contains @ treat as email, otherwise as username
                var loginUser = Input.Email.Contains('@')
                    ? await _userManager.FindByEmailAsync(Input.Email)
                    : await _userManager.FindByNameAsync(Input.Email);

                if (loginUser == null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }

                if (!loginUser.IsApproved)
                {
                    ModelState.AddModelError(string.Empty, "Your registration is awaiting administrator approval.");
                    return Page();
                }

                var result = await _signInManager.PasswordSignInAsync(loginUser.UserName, Input.Password, Input.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    if (await _userManager.IsInRoleAsync(
                            loginUser,
                            Roles.SuperAdmin.ToString()))
                    {
                        return RedirectToAction(
                            "Index",
                            "Statistics",
                            new { area = "" });
                    }

                    // SmartOps ships the company staff interface only; the
                    // separate insurer portal is not part of this integration kit.
                    if (await _userManager.IsInRoleAsync(
                            loginUser,
                            Roles.Insurer.ToString()))
                    {
                        await _signInManager.SignOutAsync();
                        ModelState.AddModelError(string.Empty,
                            "Insurer portal accounts are not supported in this web interface.");
                        return Page();
                    }

                    return RedirectToAction(
                        "Index",
                        "Jobs",
                        new { area = "", view = "week" });
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return Page();
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
