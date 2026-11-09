using SmartOps.Web.Data;
using SmartOps.Web.Models;

namespace SmartOps.Web.Services;

public sealed class AuditRequestMiddleware(RequestDelegate next, ILogger<AuditRequestMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext http, AppDBContext db, ITenantContext tenant)
    {
        if (http.GetEndpoint() is null) { await next(http); return; }
        var entry = AuditIdentity.Create(http, tenant);
        bool failed = false;
        try { await next(http); }
        catch { failed = true; throw; }
        finally
        {
            // Retain the actor on logout; pick up an authenticated principal set during login.
            if (entry.UserId is null && AuditIdentity.Principal(http)?.Identity?.IsAuthenticated == true)
                entry = AuditIdentity.Create(http, tenant);
            entry.Action = "Request";
            entry.EntityType = "Web action";
            entry.StatusCode = failed ? StatusCodes.Status500InternalServerError : http.Response.StatusCode;
            try { await db.WriteAuditEventAsync(entry, CancellationToken.None); }
            catch (Exception ex) { logger.LogCritical(ex, "Could not persist audit request {CorrelationId}", entry.CorrelationId); }
        }
    }
}
