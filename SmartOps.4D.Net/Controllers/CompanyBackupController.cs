using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = "Admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CompanyBackupController(AppDBContext db, ITenantContext tenantContext,
    IConfiguration configuration) : Controller
{
    private readonly TenantBackupService backup = new(db, configuration);

    private Task<Tenant?> CurrentTenantAsync(CancellationToken cancellationToken) =>
        db.Tenant.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId, cancellationToken);

    [HttpGet]
    public async Task<IActionResult> Manifest(CancellationToken cancellationToken)
    {
        var tenant = await CurrentTenantAsync(cancellationToken);
        if (tenant is null) return NotFound();
        return Json(new {
            tenantId = tenant.Id, tenantName = tenant.Name, exportedAtUtc = DateTime.UtcNow,
            tables = backup.GetTables().Select(table => new {
                name = table.Name, fileName = table.Name + ".json",
                url = Url.Action(nameof(Table), new { name = table.Name }) }),
            media = backup.GetMediaFiles(tenant.Id).Select(file => new {
                relativePath = file.RelativePath, size = file.Size,
                url = Url.Action(nameof(Media), new { path = file.RelativePath }) })
        });
    }

    [HttpGet]
    public async Task<IActionResult> Table(string name, CancellationToken cancellationToken)
    {
        var tenant = await CurrentTenantAsync(cancellationToken);
        if (tenant is null) return NotFound();
        var table = backup.GetTables().SingleOrDefault(table => table.Name == name);
        if (table is null) return NotFound();
        Response.ContentType = "application/json; charset=utf-8";
        await backup.WriteTableAsync(Response.Body, table, tenant.Id, cancellationToken);
        return new EmptyResult();
    }

    [HttpGet]
    public async Task<IActionResult> Media(string path, CancellationToken cancellationToken)
    {
        var tenant = await CurrentTenantAsync(cancellationToken);
        if (tenant is null) return NotFound();
        string? file = backup.ResolveMediaFile(tenant.Id, path);
        return file is null ? NotFound() : PhysicalFile(file, "application/octet-stream", enableRangeProcessing: true);
    }
}
