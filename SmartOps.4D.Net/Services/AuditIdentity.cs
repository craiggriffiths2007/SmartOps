using System.Security.Claims;
using SmartOps.Web.Models;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;

namespace SmartOps.Web.Services;

public static class AuditIdentity
{
    public static readonly object SignedInPrincipalKey = new();
    public static ClaimsPrincipal? Principal(HttpContext? http) =>
        http?.Items[SignedInPrincipalKey] as ClaimsPrincipal ?? http?.User;

    public static string Limit(string? text, int length) =>
        string.IsNullOrEmpty(text) ? "" : text[..Math.Min(text.Length, length)];

    public static AuditLog Create(HttpContext? http, ITenantContext tenant)
    {
        var principal = Principal(http);
        bool authenticated = principal?.Identity?.IsAuthenticated == true;
        var endpoint = http?.GetEndpoint();
        var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var page = endpoint?.Metadata.GetMetadata<PageActionDescriptor>();
        // Templates only. Actual URLs, query strings, bodies, cookies and tokens are never recorded.
        string route = action is not null ? $"{action.ControllerName}/{action.ActionName}"
            : page?.ViewEnginePath ?? (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "";
        string? companyName = authenticated ? principal!.FindFirstValue("TenantName") ?? tenant.TenantName : tenant.TenantName;
        return new AuditLog {
            OccurredUtc = DateTime.UtcNow,
            TenantId = authenticated && int.TryParse(principal!.FindFirstValue("TenantId"), out int companyId) ? companyId : tenant.TenantId,
            CompanyName = string.IsNullOrWhiteSpace(companyName) ? null : Limit(companyName, 200),
            UserId = authenticated ? Limit(principal!.FindFirstValue(ClaimTypes.NameIdentifier), 450) : null,
            UserName = authenticated ? Limit(principal!.Identity!.Name ?? "Authenticated user", 256)
                : http is null ? "System" : "Anonymous",
            Method = Limit(http?.Request.Method, 16),
            Route = Limit(route, 512),
            CorrelationId = Limit(http?.TraceIdentifier, 128)
        };
    }
}
