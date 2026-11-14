using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using SmartOps.Web;
using SmartOps.Web.Bridge;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
// Opt-in LAN test. Never expose the web interface by default.
var surveyProxyEnabled = builder.Configuration.GetValue<bool>("SurveyProxy:Enabled");
builder.WebHost.UseUrls(surveyProxyEnabled
    ? ["http://127.0.0.1:5245", "http://0.0.0.0:8085"]
    : [builder.Configuration["Urls"] ?? "http://127.0.0.1:5245"]);
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<BridgeSession>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<SqliteConnection>(_ => { var c = new SqliteConnection("Data Source=:memory:;Foreign Keys=False"); c.Open(); return c; });
builder.Services.AddDbContext<AppDBContext>((services, options) => options.UseSqlite(services.GetRequiredService<SqliteConnection>()));
builder.Services.AddIdentity<AppUser, IdentityRole>(options => { options.Password.RequireNonAlphanumeric = false; options.Password.RequiredLength = 12; })
    .AddEntityFrameworkStores<AppDBContext>().AddDefaultTokenProviders().AddDefaultUI();
builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Account/Login"; options.AccessDeniedPath = "/Account/Denied";
    options.Cookie.Name = "SmartOps.4D.Web"; options.ExpireTimeSpan = TimeSpan.FromMinutes(55);
    // 4D authenticates the bearer session when the bridge loads each request.
    // Identity's periodic security-stamp check would query the disposable SQLite
    // workspace before BridgeMiddleware has created and populated it.
    options.Events.OnValidatePrincipal = context => {
        if (context.Principal?.HasClaim(claim => claim.Type == "4DToken" &&
            !string.IsNullOrWhiteSpace(claim.Value)) != true)
        {
            context.RejectPrincipal();
            return context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
        return Task.CompletedTask;
    };
});
builder.Services.AddHttpClient("4D", client => {
    client.BaseAddress = new Uri(builder.Configuration["FourD:BaseUrl"] ?? FindLocalFourDAddress());
    client.Timeout = TimeSpan.FromSeconds(30);
});
if (surveyProxyEnabled)
{
    // Use the same known-good 4D destination as the web bridge. To force
    // loopback, set FourD__BaseUrl=http://127.0.0.1:8045/.
    var fourDAddress = builder.Configuration["FourD:BaseUrl"] ?? FindLocalFourDAddress();
    if (!fourDAddress.EndsWith('/')) fourDAddress += "/";

    var proxyRoutes = SurveyProxyPaths.PostPaths.Select((path, index) => new RouteConfig
    {
        RouteId = $"survey-{index}",
        ClusterId = "survey-4d",
        Order = -100,
        Match = new RouteMatch { Path = path, Methods = ["POST"] }
    }).Append(new RouteConfig
    {
        RouteId = "survey-health",
        ClusterId = "survey-4d",
        Order = -100,
        Match = new RouteMatch { Path = "/api/health", Methods = ["GET"] }
    }).ToArray();

    builder.Services.AddReverseProxy().LoadFromMemory(proxyRoutes,
    [
        new ClusterConfig
        {
            ClusterId = "survey-4d",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["4d"] = new() { Address = fourDAddress }
            }
        }
    ]);
}
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.Configure<RazorViewEngineOptions>(o => o.ViewLocationExpanders.Add(new CustomViewLocationExpander()));
builder.Services.AddScoped<TenantQuestionSetService>();
builder.Services.AddScoped<Repo>();
builder.Services.AddScoped<ITenantDeletionService, TenantDeletionService>();
builder.Services.AddScoped<TenantBackupService>();
builder.Services.AddSingleton<BuildInfoService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EmailLoginLinkService>();
builder.Services.AddTransient<IEmailSender, LocalEmailDisabled>();
var app = builder.Build();
// The LAN test port is for mobile API requests only, not the web UI.
if (surveyProxyEnabled)
{
    app.Use(async (context, next) =>
    {
        if (context.Connection.LocalPort == 8085 &&
            !SurveyProxyPaths.IsSurveyPath(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    });
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) => {
    if (context.Request.Path.StartsWithSegments("/Identity/Account/Login")) { context.Response.Redirect("/Account/Login"); return; }
    if (context.Request.Path.StartsWithSegments("/Identity/Account/Logout")) {
        await context.SignOutAsync(IdentityConstants.ApplicationScheme); context.Response.Redirect("/Account/Login"); return;
    }
    await next();
});
app.UseMiddleware<BridgeMiddleware>();
if (surveyProxyEnabled)
{
    app.MapReverseProxy();
    app.Logger.LogInformation("Survey API proxy enabled on LAN port 8085");
}
app.MapGet("/Account/Login", (bool? passwordChanged) => Results.Content(LoginHtml(
    passwordChanged == true ? "Your password has been changed. Please sign in again." : ""), "text/html"));
app.MapPost("/Account/Login", async (HttpContext context, BridgeSession bridge, IConfiguration configuration) => {
    var form = await context.Request.ReadFormAsync();
    using var client = bridge.Client();
    try {
        using var response = await client.PostAsJsonAsync(
            "api/auth/login",
            new
            {
                TenantCode = configuration["FourD:TenantCode"] ?? "SMARTOPS",
                Username = form["Username"].ToString(),
                Password = form["Password"].ToString()
            },
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = null
            });
        var data = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        if (!response.IsSuccessStatusCode) return Results.Content(LoginHtml(data["error"]?.ToString() ?? "Login failed"), "text/html", statusCode: (int)response.StatusCode);
        if (data["role"]?.ToString() is not ("Admin" or "Staff")) return Results.Content(LoginHtml("Use a company staff login for this interface."), "text/html", statusCode: 403);
        var token = data["token"]!.GetValue<string>();
        var snapshot = await bridge.GetSnapshot(token);
        var id = snapshot["tenantId"]!.ToString();
        var claims = new List<Claim> {
            new(ClaimTypes.NameIdentifier, snapshot["account"]?["Id"]?.GetValue<string>() ?? "4d-admin-" + id), new(ClaimTypes.Name, form["Username"].ToString()),
            new("TenantId", id), new("TenantCode", configuration["FourD:TenantCode"] ?? "SMARTOPS"),
            new("TenantName", data["company"]!.ToString()), new("FirstName", "Admin"), new("4DToken", token)
        };
        var roles = snapshot["account"]?["Roles"]?.AsArray()
            .Select(role => role!.GetValue<string>()) ?? new[] { data["role"]!.GetValue<string>() };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        await context.SignInAsync(IdentityConstants.ApplicationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme)));
        return Results.Redirect(claims.Any(claim => claim.Type == ClaimTypes.Role &&
            claim.Value is "Admin" or "Diary") ? "/Jobs" : "/");
    } catch (Exception error) { return Results.Content(LoginHtml("Unable to connect to 4D. Fully reopen SmartOps in 4D and run GO_StartLAN. " + error.Message), "text/html", statusCode: 502); }
});
app.MapGet("/Account/Denied", () => Results.Content("This page requires a company administrator.", statusCode: 403));
app.MapGet("/images/{filename}", async (string filename, HttpContext context, BridgeSession bridge) => {
    if (context.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
    using var client = bridge.Client();
    var snapshot = await bridge.GetSnapshot();
    var file = snapshot["media"]!.AsArray().FirstOrDefault(x => x!["Filename"]!.ToString() == filename);
    if (file is null) return Results.NotFound();
    using var response = await client.GetAsync("api/integration/media/" + file["Id"]);
    if (!response.IsSuccessStatusCode) return Results.StatusCode((int)response.StatusCode);
    new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider().TryGetContentType(filename, out var contentType);
    return Results.File(await response.Content.ReadAsByteArrayAsync(), contentType ?? "application/octet-stream", enableRangeProcessing: true);
});
app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// When launched directly from the packaged Windows executable, open the login
// page in the user's default browser after the HTTP server has started.
// Visual Studio/dotnet launches and automated smoke tests remain unaffected.
app.Lifetime.ApplicationStarted.Register(() =>
{
    if (!OperatingSystem.IsWindows() ||
        Environment.GetEnvironmentVariable("SMARTOPS_NO_BROWSER") == "1" ||
        !string.Equals(Path.GetFileName(Environment.ProcessPath),
            "SmartOps.Web.exe", StringComparison.OrdinalIgnoreCase))
        return;

    try
    {
        Process.Start(new ProcessStartInfo("http://127.0.0.1:5245/Account/Login")
        {
            UseShellExecute = true
        });
    }
    catch (Exception error)
    {
        app.Logger.LogWarning(error,
            "Could not open the browser automatically. Visit http://127.0.0.1:5245/Account/Login.");
    }
});

app.Run();

// Prefer the machine's active LAN address because 4D may bind its HTTP listener
// to that interface rather than 127.0.0.1. Developers may override this by
// setting FourD__BaseUrl (or FourD:BaseUrl in configuration).
static string FindLocalFourDAddress()
{
    try
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
            .Select(n => new
            {
                Interface = n,
                Properties = n.GetIPProperties()
            })
            .Where(n => n.Properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .SelectMany(n => n.Properties.UnicastAddresses
                .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(u.Address))
                .Select(u => new { n.Interface.NetworkInterfaceType, Address = u.Address }))
            .OrderBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
            .ToList();
        if (addresses.Count > 0)
            return "http://" + addresses[0].Address + ":8045/";
    }
    catch (NetworkInformationException)
    {
        // No usable network interface; preserve the localhost fallback.
    }
    return "http://127.0.0.1:8045/";
}

static string LoginHtml(string error = "") => """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Smart Ops — 4D login</title>
<link rel="stylesheet" href="/lib/bootstrap/dist/css/bootstrap.min.css"><link rel="stylesheet" href="/css/site.css"></head>
<body><main class="container" style="max-width:480px;margin:12vh auto"><img src="/img/cs.png" alt="Smart Ops" width="72"><h1 class="mt-3">Smart Ops</h1><p>Sign in to SMARTOPS · 4D</p>
""" + "<p class='text-danger'>" + HtmlEncoder.Default.Encode(error) + "</p>" + """
<form method="post" action="/Account/Login"><label class="form-label" for="user">Username</label><input id="user" class="form-control mb-3" name="Username" autocomplete="username" required>
<label class="form-label" for="password">Password</label><input id="password" class="form-control mb-3" type="password" name="Password" autocomplete="current-password" required>
<button class="btn btn-primary" type="submit">Sign in</button></form></main></body></html>
""";

sealed class LocalEmailDisabled : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage) => throw new InvalidOperationException("Email delivery is not enabled in this local 4D test interface.");
}
