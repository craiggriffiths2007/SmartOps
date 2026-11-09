using System.Text.Json;
using SmartOps.Shared.Models;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using SmartOps.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Controllers;

[Authorize(Roles = "Admin,Diary,SuperAdmin")]
public sealed class DynamicQuestionsController(
    AppDBContext db, ITenantContext context, TenantQuestionSetService sets) : Controller
{
    private int? ResolveTenant(int? requestedId) =>
        User.IsInRole("SuperAdmin") ? requestedId : context.TenantId;

    [HttpGet]
    public async Task<IActionResult> Index(int? tenantId)
    {
        var tenants = await db.Tenant.AsNoTracking()
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name })
            .ToListAsync();
        var id = ResolveTenant(tenantId);
        if (User.IsInRole("SuperAdmin") && !id.HasValue)
            id = tenants.FirstOrDefault()?.Id;
        if (id is null) return NotFound("No company selected.");
        var tenant = await db.Tenant.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value);
        if (tenant is null) return NotFound();
        var published = await sets.GetAsync(tenant.Id);
        var draft = await db.TenantQuestionSets.AsNoTracking()
            .Where(x => x.TenantId == tenant.Id)
            .Select(x => new { x.DraftQuestionsJson, x.DraftRevision, x.DraftSavedUtc })
            .FirstOrDefaultAsync();
        DynamicQuestionSetPackage editPackage = published;
        bool isDraft = !string.IsNullOrWhiteSpace(draft?.DraftQuestionsJson);
        if (isDraft)
        {
            try
            {
                editPackage = JsonSerializer.Deserialize<DynamicQuestionSetPackage>(
                    draft!.DraftQuestionsJson!, DynamicQuestionLoader.JsonOptions) ?? published;
                editPackage.Version = published.Version;
                DynamicQuestionCatalogue.EnsureNewCategories(editPackage);
            }
            catch (JsonException) { isDraft = false; editPackage = published; }
        }
        return RenderEditor(tenant.Id, tenant.Name, editPackage,
            tenants.Select(x => (x.Id, x.Name)).ToList(),
            draft?.DraftRevision ?? 0, draft?.DraftSavedUtc, isDraft);
    }

    private IActionResult RenderEditor(int tenantId, string name, DynamicQuestionSetPackage package,
        List<(int Id, string Name)> tenants, int revision = 0, DateTime? draftSaved = null, bool isDraft = false)
    {
        ViewBag.TenantId = tenantId;
        ViewBag.TenantName = name;
        ViewBag.Version = package.Version;
        ViewBag.DraftRevision = revision;
        ViewBag.DraftSaved = draftSaved;
        ViewBag.IsDraft = isDraft;
        ViewBag.DiagramNamesJson = JsonSerializer.Serialize(DiagramAssets.Names);
        ViewBag.Saved = package.Version > 1;
        ViewBag.SuperAdmin = User.IsInRole("SuperAdmin");
        ViewBag.Tenants = tenants;
        ViewBag.NonItemKeysJson = JsonSerializer.Serialize(DynamicQuestionCatalogue.NonItemKeys);
        ViewBag.ItemLabelsJson = JsonSerializer.Serialize(DynamicQuestionCatalogue.ItemLabels);
        ViewBag.DefaultsJson = JsonSerializer.Serialize(
            DynamicQuestionCatalogue.CreateDefaults(), DynamicQuestionLoader.JsonOptions);
        return View("Index", JsonSerializer.Serialize(package, DynamicQuestionLoader.JsonOptions));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> Save(int? tenantId, int version, int draftRevision, string questionsJson)
    {
        var id = ResolveTenant(tenantId);
        if (id is null) return Forbid();
        var tenant = await db.Tenant.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value);
        if (tenant is null) return NotFound();

        DynamicQuestionSetPackage? package;
        try
        {
            if (questionsJson?.Length > 1_000_000) throw new JsonException("Question set is too large.");
            package = JsonSerializer.Deserialize<DynamicQuestionSetPackage>(
                questionsJson ?? "", DynamicQuestionLoader.JsonOptions);
        }
        catch (JsonException ex)
        {
            ModelState.AddModelError("", "Invalid question data: " + ex.Message);
            package = await sets.GetAsync(tenant.Id);
        }

        if (package is null)
        {
            ModelState.AddModelError("", "Question data is empty.");
            package = await sets.GetAsync(tenant.Id);
        }

        var errors = DynamicQuestionCatalogue.Validate(package);
        if (errors.Count == 0) errors.AddRange(await ValidateSharedRetentionAsync(tenant.Id, package));
        foreach (var error in errors.Take(20)) ModelState.AddModelError("", error);
        if (version < 1 || version == int.MaxValue)
            ModelState.AddModelError("", "Invalid version.");
        package.Version = version;
        if (!ModelState.IsValid)
            return RenderEditor(tenant.Id, tenant.Name, package,
                await GetTenantOptionsAsync(), draftRevision);

        var newVersion = await sets.SaveAsync(tenant.Id, version, draftRevision, package);
        if (newVersion is null)
        {
            ModelState.AddModelError("", "This question set was changed in another session. Copy your changes before refreshing and reviewing the latest version.");
            return RenderEditor(tenant.Id, tenant.Name, package, await GetTenantOptionsAsync(), draftRevision);
        }

        TempData["Success"] = $"Questions saved as version {newVersion}. Devices will sync during their next job download.";
        return RedirectToAction(nameof(Index), new { tenantId = tenant.Id });
    }

    // Validate and normalize a local file without saving or publishing it.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> ValidateImport(int? tenantId, string questionsJson)
    {
        var id = ResolveTenant(tenantId);
        if (id is null) return Forbid();
        if (string.IsNullOrWhiteSpace(questionsJson) || questionsJson.Length > 1_000_000)
            return BadRequest(new { error = "Choose a non-empty question JSON file up to 1 MB." });

        DynamicQuestionSetPackage? package;
        try
        {
            package = JsonSerializer.Deserialize<DynamicQuestionSetPackage>(
                questionsJson, DynamicQuestionLoader.JsonOptions);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "The file does not contain valid question JSON." });
        }
        if (package?.Sets is null)
            return BadRequest(new { error = "The file is missing question categories." });

        var categories = new Dictionary<string, DynamicQuestionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, definition) in package.Sets)
        {
            var canonicalKey = DynamicQuestionCatalogue.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? key;
            if (!categories.TryAdd(canonicalKey, definition))
                return BadRequest(new { error = $"Duplicate question category: {key}." });
        }
        package.Sets = categories;
        var errors = DynamicQuestionCatalogue.Validate(package);
        if (errors.Count == 0)
        {
            DynamicQuestionCatalogue.EnsureNewCategories(package);
            errors = DynamicQuestionCatalogue.Validate(package);
        }
        if (errors.Count == 0) errors.AddRange(await ValidateSharedRetentionAsync(id.Value, package));
        if (errors.Count > 0)
            return BadRequest(new { error = string.Join("; ", errors.Take(5)) });

        // Use canonical category names; the editor retains its own version and draft revision.
        package.Sets = categories;
        return new JsonResult(package, DynamicQuestionLoader.JsonOptions);
    }

    // Ajax: same validation as publishing, but does not increment the device version.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2_000_000)]
    public async Task<IActionResult> SaveDraft(int? tenantId, int version, int draftRevision, string questionsJson)
    {
        var id = ResolveTenant(tenantId);
        if (id is null) return Forbid();
        if (!await db.Tenant.AsNoTracking().AnyAsync(x => x.Id == id.Value)) return NotFound();
        if (questionsJson?.Length > 1_000_000 || version < 1 || draftRevision < 0 ||
            draftRevision == int.MaxValue)
            return BadRequest(new { error = "Invalid draft parameters." });
        DynamicQuestionSetPackage? package;
        try { package = JsonSerializer.Deserialize<DynamicQuestionSetPackage>(
            questionsJson ?? "", DynamicQuestionLoader.JsonOptions); }
        catch (JsonException) { return BadRequest(new { error = "Invalid question JSON." }); }
        if (package is null) return BadRequest(new { error = "Empty draft." });
        var errors = DynamicQuestionCatalogue.Validate(package);
        if (errors.Count == 0) errors.AddRange(await ValidateSharedRetentionAsync(id.Value, package));
        if (errors.Count > 0) return BadRequest(new { error = string.Join("; ", errors.Take(5)) });
        var next = await sets.SaveDraftAsync(id.Value, version, draftRevision, package);
        return next is null
            ? Conflict(new { error = "Another editor changed these questions. Your unsaved edits remain on screen; copy them before refreshing." })
            : Ok(new { revision = next.Value, savedAtUtc = DateTime.UtcNow.ToString("O") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscardDraft(int? tenantId, int version, int draftRevision)
    {
        var id = ResolveTenant(tenantId);
        if (id is null) return Forbid();
        bool ok = await sets.DiscardDraftAsync(id.Value, version, draftRevision);
        return ok ? Ok(new { discarded = true }) : Conflict(new { error = "Draft changed in another session." });
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAll(int? tenantId, int version, int draftRevision)
    {
        var id = ResolveTenant(tenantId);
        if (id is null) return Forbid();
        var tenant = await db.Tenant.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value);
        if (tenant is null) return NotFound();
        if (!await sets.ResetAllAsync(tenant.Id, version, draftRevision))
            return Conflict(new { error = "Questions changed in another session. Refresh and review them before resetting all questions." });
        TempData["Success"] = $"All questions for {tenant.Name} have been reset to defaults as version 1. Unpublished changes have been discarded.";
        return Ok(new { reset = true });
    }

    private async Task<List<string>> ValidateSharedRetentionAsync(int tenantId, DynamicQuestionSetPackage package)
    {
        var previous = await sets.GetAsync(tenantId);
        var draftJson = await db.TenantQuestionSets.AsNoTracking().Where(x => x.TenantId == tenantId)
            .Select(x => x.DraftQuestionsJson).FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(draftJson))
        {
            try
            {
                previous = JsonSerializer.Deserialize<DynamicQuestionSetPackage>(draftJson, DynamicQuestionLoader.JsonOptions) ?? previous;
                DynamicQuestionCatalogue.EnsureNewCategories(previous);
            }
            catch (JsonException) { }
        }
        return SharedItemQuestions.ValidateRetention(previous, package);
    }

    private async Task<List<(int Id, string Name)>> GetTenantOptionsAsync() =>
        (await db.Tenant.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name }).ToListAsync())
        .Select(x => (x.Id, x.Name)).ToList();
}
