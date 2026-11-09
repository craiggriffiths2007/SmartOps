// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using SmartOps.Web.Data;

namespace SmartOps.Web.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;

        public ForgotPasswordModel(
            UserManager<AppUser> userManager,
            IEmailSender emailSender,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _configuration = configuration;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Don't reveal whether the account exists.
            if (user == null)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            // If confirmed email is required, don't allow password
            // reset for an unconfirmed email address.
            if (_userManager.Options.SignIn.RequireConfirmedEmail &&
                !await _userManager.IsEmailConfirmedAsync(user))
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var emailEnabled =
                _configuration.GetValue<bool>("EmailSettings:Enabled");

            if (!emailEnabled)
            {
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    code
                },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                Input.Email,
                "Reset your Mart Ops password",
                $"""
                <h2>Reset your Mart Ops password</h2>

                <p>We received a request to reset your password.</p>

                <p>
                    <a href="{HtmlEncoder.Default.Encode(callbackUrl!)}">
                        Reset my password
                    </a>
                </p>

                <p>
                    If you didn't request a password reset,
                    you can ignore this email.
                </p>
                """);

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}