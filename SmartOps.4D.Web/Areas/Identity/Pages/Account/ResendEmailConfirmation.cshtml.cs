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
    public class ResendEmailConfirmationModel : PageModel
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _configuration;

        public ResendEmailConfirmationModel(
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

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Don't reveal whether the user exists.
            if (user == null)
            {
                StatusMessage =
                    "If an account exists for that email address, a confirmation email has been sent.";

                return RedirectToPage();
            }

            if (await _userManager.IsEmailConfirmedAsync(user))
            {
                StatusMessage =
                    "That email address has already been confirmed.";

                return RedirectToPage();
            }

            var emailEnabled =
                _configuration.GetValue<bool>("EmailSettings:Enabled");

            if (!emailEnabled)
            {
                StatusMessage =
                    "Email sending is currently unavailable.";

                return RedirectToPage();
            }

            var userId =
                await _userManager.GetUserIdAsync(user);

            var code =
                await _userManager.GenerateEmailConfirmationTokenAsync(user);

            code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(code));

            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new
                {
                    area = "Identity",
                    userId,
                    code
                },
                protocol: Request.Scheme);

            await _emailSender.SendEmailAsync(
                Input.Email,
                "Confirm your Mart Ops account",
                $"""
                <h2>Confirm your Mart Ops account</h2>

                <p>Please confirm your email address by clicking the link below:</p>

                <p>
                    <a href="{HtmlEncoder.Default.Encode(callbackUrl!)}">
                        Confirm my email
                    </a>
                </p>

                <p>
                    If you didn't create this account, you can ignore this email.
                </p>
                """);

            StatusMessage =
                "If an account exists for that email address, a confirmation email has been sent.";

            return RedirectToPage();
        }
    }
}