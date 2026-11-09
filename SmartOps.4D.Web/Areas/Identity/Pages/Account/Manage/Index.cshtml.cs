// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartOps.Web.Data;
using SkiaSharp;

namespace SmartOps.Web.Areas.Identity.Pages.Account.Manage
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class IndexModel : PageModel
    {
        private readonly SmartOps.Web.Bridge.AccountService _account;

        public IndexModel(SmartOps.Web.Bridge.AccountService account) => _account = account;

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Username { get; set; }

        public byte[] ProfilePicture { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
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
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }

            [Display(Name = "Profile Picture")]
            public IFormFile Photo { get; set; }
        }

        private async Task LoadAsync(bool loadInput = true)
        {
            var profile = await _account.GetProfile();
            Username = profile.Username;
            ProfilePicture = string.IsNullOrEmpty(profile.ProfilePicture)
                ? null : Convert.FromBase64String(profile.ProfilePicture);
            if (loadInput) Input = new InputModel { PhoneNumber = profile.PhoneNumber };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                try
                {
                    byte[] picture = null;
                    if (Input.Photo != null && Input.Photo.Length > 0)
                    {
                        if (Input.Photo.Length > 8000000)
                            throw new SmartOps.Web.Bridge.AccountValidationException("Choose a profile photo smaller than 8 MB.");
                        picture = await ResizeProfilePicture(Input.Photo);
                    }
                    await _account.SaveProfile(Input.PhoneNumber, picture);
                    StatusMessage = "Your profile has been updated";
                    return RedirectToPage();
                }
                catch (Exception error) when (error is SmartOps.Web.Bridge.AccountValidationException or HttpRequestException or InvalidOperationException)
                {
                    ModelState.AddModelError(string.Empty, error.Message);
                }
            }
            await LoadAsync(loadInput: false);
            return Page();
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
