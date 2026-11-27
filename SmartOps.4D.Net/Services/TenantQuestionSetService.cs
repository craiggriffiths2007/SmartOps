using System.Text.Json;
using SmartOps.Shared.Models;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Services;

public sealed class TenantQuestionSetService(AppDBContext db)
{
    private readonly Dictionary<int, DynamicQuestionSetPackage> _loaded = new();

    public async Task<DynamicQuestionDefinition> GetDefinitionAsync(int? tenantId, string key)
    {
        if (tenantId is not int id) return DynamicQuestionLoader.LoadDefault(key);
        var package = await GetAsync(id);
        return package.Sets.TryGetValue(key, out var definition)
            ? SharedItemQuestions.Compose(key, definition, package)
            : DynamicQuestionLoader.LoadDefault(key);
    }

    public async Task<int> GetVersionAsync(int tenantId)
    {
        return await db.TenantQuestionSets.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => (int?)x.Version).FirstOrDefaultAsync() ?? 1;
    }

    public async Task<DynamicQuestionSetPackage> GetAsync(int tenantId)
    {
        if (_loaded.TryGetValue(tenantId, out var cached)) return cached;
        var row = await db.TenantQuestionSets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId);
        if (row is null)
            return _loaded[tenantId] = DynamicQuestionCatalogue.CreateDefaults();

        var data = JsonSerializer.Deserialize<DynamicQuestionSetPackage>(
            row.QuestionsJson, DynamicQuestionLoader.JsonOptions)
            ?? throw new InvalidOperationException("The saved question set is invalid.");
        data.Version = row.Version;
        DynamicQuestionCatalogue.EnsureNewCategories(data);
        return _loaded[tenantId] = data;
    }

    // Drafts use their own revision so autosave cannot overwrite another editor's work.
    public async Task<int?> SaveDraftAsync(int tenantId, int version, int revision,
        DynamicQuestionSetPackage package)
    {
        package.Version = version;
        string json = JsonSerializer.Serialize(package, DynamicQuestionLoader.JsonOptions);
        var utc = DateTime.UtcNow;
        int count = await db.AuditBulkAsync("TenantQuestionSets", tenantId, "Updated", () => db.TenantQuestionSets
            .Where(x => x.TenantId == tenantId && x.Version == version && x.DraftRevision == revision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DraftQuestionsJson, json)
                .SetProperty(x => x.DraftSavedUtc, utc)
                .SetProperty(x => x.DraftRevision, revision + 1)));
        if (count == 1) return revision + 1;

        if (version != 1 || revision != 0 ||
            await db.TenantQuestionSets.AnyAsync(x => x.TenantId == tenantId))
            return null;

        var defaults = DynamicQuestionCatalogue.CreateDefaults();
        db.TenantQuestionSets.Add(new TenantQuestionSet {
            TenantId = tenantId, Version = 1,
            QuestionsJson = JsonSerializer.Serialize(defaults, DynamicQuestionLoader.JsonOptions),
            DraftQuestionsJson = json, DraftSavedUtc = utc, DraftRevision = 1,
            UpdatedUtc = utc
        });
        try { await db.SaveChangesAsync(); return 1; }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
            (sql.Number == 2601 || sql.Number == 2627)) { return null; }
    }

    // Keep the draft revision increasing even when the published version returns to V1.
    public async Task<bool> ResetAllAsync(int tenantId, int version, int revision)
    {
        if (version < 1 || revision < 0 || revision == int.MaxValue) return false;
        var defaults = DynamicQuestionCatalogue.CreateDefaults();
        defaults.Version = 1;
        string json = JsonSerializer.Serialize(defaults, DynamicQuestionLoader.JsonOptions);
        int count = await db.AuditBulkAsync("TenantQuestionSets", tenantId, "Updated", () => db.TenantQuestionSets
            .Where(x => x.TenantId == tenantId && x.Version == version && x.DraftRevision == revision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.QuestionsJson, json)
                .SetProperty(x => x.Version, 1)
                .SetProperty(x => x.DraftQuestionsJson, (string?)null)
                .SetProperty(x => x.DraftSavedUtc, (DateTime?)null)
                .SetProperty(x => x.DraftRevision, revision + 1)
                .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow)));
        if (count == 0 && (version != 1 || revision != 0 ||
            await db.TenantQuestionSets.AnyAsync(x => x.TenantId == tenantId))) return false;
        _loaded[tenantId] = defaults;
        return true;
    }

    public async Task<bool> DiscardDraftAsync(int tenantId, int version, int revision)
    {
        return await db.AuditBulkAsync("TenantQuestionSets", tenantId, "Updated", () => db.TenantQuestionSets.Where(x => x.TenantId == tenantId &&
            x.Version == version && x.DraftRevision == revision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.DraftQuestionsJson, (string?)null)
                .SetProperty(x => x.DraftSavedUtc, (DateTime?)null)
                .SetProperty(x => x.DraftRevision, revision + 1))) == 1;
    }

    /// <summary>Optimistic compare-and-swap: two editors cannot silently overwrite each other.</summary>
    public async Task<int?> SaveAsync(int tenantId, int expectedVersion, int expectedDraftRevision, DynamicQuestionSetPackage package)
    {
        package.Version = checked(expectedVersion + 1);
        string json = JsonSerializer.Serialize(package, DynamicQuestionLoader.JsonOptions);
        int modified = await db.AuditBulkAsync("TenantQuestionSets", tenantId, "Updated", () => db.TenantQuestionSets
            .Where(x => x.TenantId == tenantId && x.Version == expectedVersion && x.DraftRevision == expectedDraftRevision)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.QuestionsJson, json)
                .SetProperty(x => x.Version, package.Version)
                .SetProperty(x => x.DraftQuestionsJson, (string?)null)
                .SetProperty(x => x.DraftSavedUtc, (DateTime?)null)
                .SetProperty(x => x.DraftRevision, expectedDraftRevision + 1)
                .SetProperty(x => x.UpdatedUtc, DateTime.UtcNow)));
        if (modified == 1)
        {
            _loaded[tenantId] = package;
            return package.Version;
        }

        if (expectedVersion != 1 || expectedDraftRevision != 0 || await db.TenantQuestionSets.AnyAsync(x => x.TenantId == tenantId))
            return null;
        db.TenantQuestionSets.Add(new TenantQuestionSet
        {
            TenantId = tenantId,
            Version = package.Version,
            QuestionsJson = json,
            UpdatedUtc = DateTime.UtcNow
        });
        try
        {
            await db.SaveChangesAsync();
            _loaded[tenantId] = package;
            return package.Version;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
            (sql.Number == 2601 || sql.Number == 2627))
        {
            // Unique-key collision from two editors publishing the first version.
            return null;
        }
    }
}
