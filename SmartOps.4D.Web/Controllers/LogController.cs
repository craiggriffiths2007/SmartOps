using SmartOps.Web.Data;
using SmartOps.Web.Helpers;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = "Admin,SuperAdmin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LogController(AppDBContext db, ITenantContext tenant) : Controller
{
    // Every list, filter source, GET and POST uses the same company boundary.
    private IQueryable<AuditLog> AccessibleLogs() => User.IsInRole("SuperAdmin")
        ? db.AuditLogs.IgnoreQueryFilters()
        : db.AuditLogs.Where(e => tenant.TenantId != null && e.TenantId == tenant.TenantId);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AuditLogFilter filter, CancellationToken token)
    {
        var model = new AuditLogIndexViewModel { Filter = filter, IsSuperAdmin = User.IsInRole("SuperAdmin") };
        var scope = AccessibleLogs().AsNoTracking();
        if (model.IsSuperAdmin)
        {
            var companies = await scope.Where(e => e.TenantId != null)
                .GroupBy(e => e.TenantId).Select(g => new { Id = g.Key, Name = g.Max(e => e.CompanyName) }).ToListAsync(token);
            model.Companies = companies.OrderBy(x => x.Name).Select(x => new SelectListItem(
                x.Name ?? $"Company #{x.Id}", x.Id.ToString())).ToList();
            if (filter.TenantId.HasValue) scope = scope.Where(e => e.TenantId == filter.TenantId);
        }
        else filter.TenantId = tenant.TenantId; // Ignore any forged company filter.

        var actors = await scope.Select(e => new { e.UserId, e.UserName }).Distinct().ToListAsync(token);
        model.Users = actors.GroupBy(x => x.UserId is null ? "name:" + x.UserName : "id:" + x.UserId)
            .Select(g => new SelectListItem(g.First().UserName, g.Key)).OrderBy(x => x.Text).ToList();
        model.Actions = (await scope.Select(e => e.Action).Distinct().OrderBy(x => x).ToListAsync(token))
            .Select(x => new SelectListItem(x, x)).ToList();
        model.RecordTypes = (await scope.Select(e => e.EntityType).Distinct().OrderBy(x => x).ToListAsync(token))
            .Select(x => new SelectListItem(x, x)).ToList();

        if (filter.From.HasValue && filter.To.HasValue && filter.From.Value.Date > filter.To.Value.Date)
            ModelState.AddModelError("Filter.To", "End date must be on or after start date.");
        if (!ModelState.IsValid) return View(model);
        if (filter.DatabaseUpdatesOnly)
            scope = scope.Where(e => e.Action == "Created" || e.Action == "Updated"
                || e.Action == "Deleted" || e.Action == "Log note edited");
        if (!string.IsNullOrWhiteSpace(filter.Actor))
        {
            if (filter.Actor.StartsWith("id:", StringComparison.Ordinal))
            { string id = filter.Actor[3..]; scope = scope.Where(e => e.UserId == id); }
            else if (filter.Actor.StartsWith("name:", StringComparison.Ordinal))
            { string name = filter.Actor[5..]; scope = scope.Where(e => e.UserId == null && e.UserName == name); }
            else return BadRequest("Invalid user filter.");
        }
        if (!string.IsNullOrWhiteSpace(filter.EventAction)) scope = scope.Where(e => e.Action == filter.EventAction);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) scope = scope.Where(e => e.EntityType == filter.EntityType);
        if (filter.From.HasValue) { var from = AuditLogTime.StartOfDayUtc(filter.From.Value); scope = scope.Where(e => e.OccurredUtc >= from); }
        if (filter.To.HasValue && filter.To.Value.Date < DateTime.MaxValue.Date)
        { var until = AuditLogTime.StartOfDayUtc(filter.To.Value.Date.AddDays(1)); scope = scope.Where(e => e.OccurredUtc < until); }
        if (filter.Outcome == "failure") scope = scope.Where(e => e.StatusCode >= 400);
        if (filter.Outcome == "success") scope = scope.Where(e => e.StatusCode < 400 && e.StatusCode != null);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            string text = filter.Search.Trim();
            scope = scope.Where(e => e.UserName.Contains(text) || e.EntityId.Contains(text)
                || e.Route.Contains(text) || e.ChangedFields.Contains(text) || e.Notes.Contains(text)
                || e.CorrelationId.Contains(text) || (e.CompanyName != null && e.CompanyName.Contains(text)));
        }
        model.Total = await scope.CountAsync(token);
        filter.Page = Math.Min(filter.Page, model.PageCount);
        model.Entries = await scope.OrderByDescending(e => e.OccurredUtc).ThenByDescending(e => e.Id)
            .Skip((filter.Page - 1) * 50).Take(50).ToListAsync(token);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id, CancellationToken token)
    {
        var entry = await AccessibleLogs().AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, token);
        return entry is null ? NotFound() : View(new AuditLogEditViewModel {
            Id = id, Revision = entry.Revision, Notes = entry.Notes, Entry = entry
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, AuditLogEditViewModel model, CancellationToken token)
    {
        if (id != model.Id) return BadRequest();
        var entry = await AccessibleLogs().SingleOrDefaultAsync(e => e.Id == id, token);
        if (entry is null) return NotFound();
        model.Entry = entry;
        if (!ModelState.IsValid) return View(model);
        db.Entry(entry).Property(e => e.Revision).OriginalValue = model.Revision;
        entry.Notes = model.Notes?.Trim() ?? "";
        entry.Revision = Guid.NewGuid();
        try { await db.SaveChangesAsync(token); }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError("", "This log entry was edited by another admin. Reload the page before saving again.");
            return View(model);
        }
        TempData["Success"] = "Log notes saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }
}
