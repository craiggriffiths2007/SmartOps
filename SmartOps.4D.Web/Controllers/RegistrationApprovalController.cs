using SmartOps.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = "SuperAdmin")]
public class RegistrationApprovalController : Controller
{
    private readonly UserManager<AppUser> _users;
    private readonly AppDBContext _db;
    private readonly IEmailSender _email;
    private readonly ILogger<RegistrationApprovalController> _logger;

    public RegistrationApprovalController(UserManager<AppUser> users, AppDBContext db,
        IEmailSender email, ILogger<RegistrationApprovalController> logger)
    { _users = users; _db = db; _email = email; _logger = logger; }

    [HttpGet]
    public async Task<IActionResult> Review(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user?.TenantId == null || !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        var tenant = await _db.Tenant.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId);
        if (tenant == null) return NotFound();
        ViewBag.Tenant = tenant;
        return View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user?.TenantId == null || !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        var tenant = await _db.Tenant.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == user.TenantId);
        if (tenant == null) return NotFound();
        if (user.IsApproved) return RedirectToAction(nameof(Review), new { userId });

        // Save approval before sending: failed SMTP must not create a second account.
        user.IsApproved = true;
        user.ApprovedAt = DateTime.UtcNow;
        tenant.IsActive = true;
        var update = await _users.UpdateAsync(user);
        if (!update.Succeeded) return StatusCode(500, "Could not approve registration.");
        await _db.SaveChangesAsync();
        await SendApprovalEmailAsync(user);
        return RedirectToAction(nameof(Review), new { userId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendApprovalEmail(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user?.TenantId == null || !user.IsApproved ||
            !await _users.IsInRoleAsync(user, "Admin")) return NotFound();
        await SendApprovalEmailAsync(user);
        return RedirectToAction(nameof(Review), new { userId });
    }

    private async Task SendApprovalEmailAsync(AppUser user)
    {
        try
        {
            if (_users.Options.SignIn.RequireConfirmedEmail && !user.EmailConfirmed)
            {
                var token = await _users.GenerateEmailConfirmationTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var url = Url.Page("/Account/ConfirmEmail", null,
                    new { area = "Identity", userId = user.Id, code }, Request.Scheme);
                await _email.SendEmailAsync(user.Email!, "Confirm your Mart Ops account",
                    $"<p>Your registration has been approved. Please <a href=\"{HtmlEncoder.Default.Encode(url!)}\">confirm your email address</a> to sign in.</p>");
                TempData["Success"] = "Approved. Customer confirmation email sent.";
            }
            else
            {
                var url = Url.Page("/Account/Login", null,
                    new { area = "Identity" }, Request.Scheme);
                await _email.SendEmailAsync(user.Email!, "Your Mart Ops registration has been approved",
                    $"<p>Your registration has been approved. You can now <a href=\"{HtmlEncoder.Default.Encode(url!)}\">sign in</a> using your registered credentials.</p>");
                TempData["Success"] = "Approved. Customer sign-in email sent.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to send approval email to {UserId}", user.Id);
            TempData["Error"] = "Approved, but the notification email could not be sent. Use Resend approval email.";
        }
    }
}
