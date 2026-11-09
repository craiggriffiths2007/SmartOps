using SmartOps.Web.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Net;
namespace SmartOps.Web.Bridge;

public sealed class BridgeMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context, AppDBContext db, BridgeSession bridge)
    {
        if (context.User.Identity?.IsAuthenticated != true || context.Request.Path.StartsWithSegments("/images") || context.Request.Path.StartsWithSegments("/Account") || context.Request.Path.StartsWithSegments("/FourDErrorLog"))
        { await next(context); return; }
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await bridge.Load(db);
            await next(context);
            if (context.Response.StatusCode < 400)
            {
                var ids = await bridge.Commit(db);
                // Original controllers redirect using their workspace-generated
                // key. Substitute the authoritative 4D key for the next request.
                if (context.Response.Headers.Location.FirstOrDefault() is string location)
                {
                    var controller = context.GetRouteValue("controller")?.ToString();
                    foreach (var mapping in ids)
                    {
                        var split = mapping.Key.Split(':', 2);
                        if (split[0].Equals(controller, StringComparison.OrdinalIgnoreCase) ||
                            (controller == "Customers" && split[0] == "Customer") ||
                            (controller == "Contracts" && split[0] == "Contract") ||
                            (controller == "Jobs" && split[0] == "Job"))
                        {
                            location = System.Text.RegularExpressions.Regex.Replace(location,
                                @"(?<=/)" + System.Text.RegularExpressions.Regex.Escape(split[1]) + @"(?=$|[?/#])", mapping.Value!.ToString());
                        }
                    }
                    context.Response.Headers.Location = location;
                }
            }
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
        }
        catch (Exception error)
        {
            context.Response.Body = originalBody;
            // A rejected 4D bearer token means the web login is no longer
            // valid. Never present this as a database connection failure.
            if (error is InvalidOperationException invalid &&
                (invalid.Message.Contains("4D returned 401:", StringComparison.OrdinalIgnoreCase) ||
                 invalid.Message.Contains("4D returned 403:", StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.Clear();
                await context.SignOutAsync(IdentityConstants.ApplicationScheme);
                if (context.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsJsonAsync(new { error = "Session expired. Sign in again." });
                }
                else
                {
                    context.Response.Redirect("/Account/Login");
                }
                return;
            }
            context.Response.Clear();
            context.Response.StatusCode = 502;
            if (context.Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
            {
                await context.Response.WriteAsJsonAsync(new { error = "4D connection or save failed: " + error.Message });
                context.RequestServices.GetRequiredService<ILogger<BridgeMiddleware>>().LogError(error, "4D bridge request failed");
                return;
            }
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("<!doctype html><html><head><link rel='stylesheet' href='/css/site.css'></head><body style='padding:2rem'><h1>4D connection or save failed</h1><p>" +
                System.Net.WebUtility.HtmlEncode(error.Message) + "</p><p>The request workspace has been discarded. Reload from 4D before retrying.</p><a href='/'>Return to Mart Ops</a></body></html>");
            context.RequestServices.GetRequiredService<ILogger<BridgeMiddleware>>().LogError(error, "4D bridge request failed");
        }
        finally { context.Response.Body = originalBody; }
    }
}
