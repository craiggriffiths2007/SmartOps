using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Web.Data;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<AppUser> _userManager;

    public ProfileController(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Picture()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.ProfilePicture == null ||
            user.ProfilePicture.Length == 0)
        {
            return NotFound();
        }

        // Allow browser to cache it for 1 hour
        Response.Headers.CacheControl = "private,max-age=3600";

        return File(user.ProfilePicture, "image/jpeg");
    }
}