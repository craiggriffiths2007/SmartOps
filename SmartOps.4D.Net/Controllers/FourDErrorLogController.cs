using System.Text.Json;
using SmartOps.Web.Bridge;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FourDErrorLogController(BridgeSession bridge, ILogger<FourDErrorLogController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var model = new FourDErrorLogViewModel();
        try
        {
            using var client = bridge.Client();
            using var response = await client.GetAsync("api/error-log", token);
            response.EnsureSuccessStatusCode();
            model = await response.Content.ReadFromJsonAsync<FourDErrorLogViewModel>(token)
                ?? throw new JsonException("4D returned an empty error log response.");
        }
        catch (Exception error) when (error is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogWarning(error, "Unable to read the 4D error log");
            model.LoadError = "The 4D error log is unavailable. Check that 4D is running with the latest project sources, then refresh.";
        }
        return View(model);
    }
}
