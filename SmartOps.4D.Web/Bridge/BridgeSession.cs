using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using SmartOps.Web.Data;
using SmartOps.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SmartOps.Web.Bridge;

// The SQLite database is a disposable, per-request query workspace for the
// existing EF/Razor code. It is never a second authoritative business database.
public sealed class BridgeSession(IHttpClientFactory clients, IHttpContextAccessor accessor)
{
    public bool Importing { get; set; }
    public JsonObject Snapshot { get; private set; } = new();
    private readonly Dictionary<string, JsonObject> initial = new();
    private readonly Dictionary<string, IEntityType> models = new();
    private readonly Dictionary<string, HashSet<string>> fields = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    public HttpClient Client(string? token = null)
    {
        var client = clients.CreateClient("4D");
        token ??= accessor.HttpContext?.User.FindFirst("4DToken")?.Value;
        if (!string.IsNullOrEmpty(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
    public async Task<JsonObject> GetSnapshot(string? token = null)
    {
        using var client = Client(token);
        using var response = await client.GetAsync("api/web/snapshot");
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"4D returned {(int)response.StatusCode}: {text}");
        return JsonNode.Parse(text)?.AsObject() ?? throw new InvalidOperationException("4D returned an empty snapshot");
    }
    public async Task Load(AppDBContext db)
    {
        Importing = true;
        db.BridgeImporting = true;
        Snapshot = await GetSnapshot();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
        var tables = Snapshot["tables"]!.AsObject();
        var selectedEstimates = new List<(Contract Contract, int EstimateId)>();
        foreach (var model in db.Model.GetEntityTypes().Where(e => !e.IsOwned()))
        {
            var table = model.GetTableName()!;
            if (table.StartsWith("AspNet") || !tables.ContainsKey(table)) continue;
            models[table] = model;
            var source = tables[table]!.AsArray();
            fields[table] = source.Count > 0 ? source[0]!.AsObject().Select(x => x.Key).ToHashSet() :
                model.GetProperties().Where(p => p.PropertyInfo is not null).Select(p => p.Name).ToHashSet();
            foreach (var node in source)
            {
                var entity = Activator.CreateInstance(model.ClrType)!;
                foreach (var property in model.GetProperties())
                {
                    if (property.PropertyInfo?.CanWrite != true || !node!.AsObject().TryGetPropertyValue(property.Name, out var value)) continue;
                    try { property.PropertyInfo.SetValue(entity, ConvertValue(value, property.ClrType)); }
                    catch (Exception error) { throw new InvalidOperationException($"Cannot read 4D field {table}.{property.Name}: {value}", error); }
                }
                // Break the circular Contract -> Estimate -> Contract relationship
                // while importing the 4D snapshot.
                if (entity is Contract contract &&
                    contract.SelectedEstimateId.HasValue)
                {
                    selectedEstimates.Add(
                        (contract, contract.SelectedEstimateId.Value));

                    contract.SelectedEstimateId = null;
                }

                db.Add(entity);
            }
        }
        // Files are retained in 4D's tenant-scoped media folder; the original
        // report templates keep their /images/<filename> URLs via our proxy.
        foreach (var media in Snapshot["media"]!.AsArray())
        {
            var filename = media!["Filename"]!.GetValue<string>();
            if (db.ChangeTracker.Entries<PhotoImage>().Any(e => e.Entity.Filename == filename)) continue;
            db.Images.Add(new PhotoImage {
                Id = 1000000000 + media["Id"]!.GetValue<int>(), Filename = filename,
                TenantId = Snapshot["tenantId"]!.GetValue<int>(),
                DateTime = ParseDate(media["ReceivedAt"]?.ToString()), CustomerVisible = true
            });
        }
        var principal = accessor.HttpContext!.User;
        var currentId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var roleNames = SmartOps.Web.Helpers.StaffRoles.All.Concat(
            principal.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value)).Distinct().ToArray();
        foreach (var role in roleNames)
            db.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole { Id = role, Name = role, NormalizedName = role.ToUpperInvariant() });

        foreach (var node in Snapshot["users"]?.AsArray() ?? new JsonArray())
        {
            var row = node!.AsObject();
            var username = row["UserName"]!.GetValue<string>();
            var appUser = new AppUser {
                Id = row["Id"]!.GetValue<string>(), UserName = username, NormalizedUserName = username.ToUpperInvariant(),
                Email = row["Email"]?.GetValue<string>(), NormalizedEmail = row["Email"]?.GetValue<string>()?.ToUpperInvariant(),
                FirstName = row["FirstName"]?.GetValue<string>(), LastName = row["LastName"]?.GetValue<string>(),
                PhoneNumber = row["PhoneNumber"]?.GetValue<string>(), EmailConfirmed = true,
                BranchCode = row["BranchCode"]?.GetValue<string>(),
                LockBranch = row["LockBranch"]?.GetValue<bool>() ?? false,
                TenantId = Snapshot["tenantId"]!.GetValue<int>(), IsApproved = true, SecurityStamp = "4d-session"
            };
            var picture = row["ProfilePicture"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(picture)) appUser.ProfilePicture = Convert.FromBase64String(picture);
            db.Users.Add(appUser);
            foreach (var role in row["Roles"]!.AsArray().Select(x => x!.GetValue<string>()).Where(roleNames.Contains).Distinct())
                db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = appUser.Id, RoleId = role });
        }
        // Support cookies issued before account-specific IDs were introduced.
        if (!db.ChangeTracker.Entries<AppUser>().Any(e => e.Entity.Id == currentId))
        {
            var appUser = new AppUser {
                Id = currentId, UserName = principal.Identity!.Name,
                NormalizedUserName = principal.Identity.Name!.ToUpperInvariant(),
                FirstName = principal.Identity.Name, Email = "local@smartops.invalid", EmailConfirmed = true,
                TenantId = Snapshot["tenantId"]!.GetValue<int>(), IsApproved = true, SecurityStamp = "4d-session"
            };
            if (Snapshot["account"] is JsonObject profile)
            {
                appUser.PhoneNumber = profile["PhoneNumber"]?.GetValue<string>();
                appUser.BranchCode = profile["BranchCode"]?.GetValue<string>();
                appUser.LockBranch = profile["LockBranch"]?.GetValue<bool>() ?? false;
                var picture = profile["ProfilePicture"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(picture)) appUser.ProfilePicture = Convert.FromBase64String(picture);
            }
            // Avoid a duplicate normalized username for an old cookie.
            var old = db.ChangeTracker.Entries<AppUser>().FirstOrDefault(e => e.Entity.NormalizedUserName == appUser.NormalizedUserName);
            if (old is not null) appUser.NormalizedUserName += "-LEGACY";
            db.Users.Add(appUser);
            foreach (var role in principal.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).Distinct())
                db.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string> { UserId = currentId, RoleId = role });
        }
        // First save: contracts and estimates can now be inserted
        // without the circular dependency.
        await db.SaveChangesAsync();

        // Restore selected estimates after the related records exist.
        foreach (var (contract, estimateId) in selectedEstimates)
        {
            contract.SelectedEstimateId = estimateId;
        }

        // Second save: update the selected estimate relationships.
        if (selectedEstimates.Count > 0)
        {
            await db.SaveChangesAsync();
        }

        db.ChangeTracker.Clear();
        Importing = false;
        db.BridgeImporting = false;
        foreach (var table in models.Keys) initial[table] = ReadRows(db, table);
    }
    private static DateTime ParseDate(string? text) => DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.RoundtripKind, out var date) ? date : DateTime.MinValue;
    private static object? ConvertValue(JsonNode? node, Type propertyType)
    {
        if (node is null) return propertyType.IsValueType && Nullable.GetUnderlyingType(propertyType) is null ? Activator.CreateInstance(propertyType) : null;
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(DateTime)) return ParseDate(node.ToString());
        if (type == typeof(DateTimeOffset)) return DateTimeOffset.Parse(node.ToString(), CultureInfo.GetCultureInfo("en-GB"));
        if (type == typeof(Guid)) return Guid.TryParse(node.ToString(), out var guid) ? guid : Guid.Empty;
        if (type.IsEnum) return Enum.ToObject(type, int.Parse(node.ToString(), CultureInfo.InvariantCulture));
        if (type == typeof(string)) return node.ToString();
        return node.Deserialize(propertyType, Json);
    }
    private JsonObject ReadRows(AppDBContext db, string table)
    {
        var result = new JsonObject();
        var model = models[table];
        var key = model.FindPrimaryKey()!.Properties.Single();
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT * FROM \"" + table.Replace("\"", "\"\"") + "\"";
        using var reader = command.ExecuteReader();
        var properties = model.GetProperties().Where(p => fields[table].Contains(p.Name) && p.GetColumnName() is not null).ToArray();
        while (reader.Read())
        {
            var row = new JsonObject();
            foreach (var property in properties)
            {
                var value = reader[property.GetColumnName()!];
                if (value is DBNull) row[property.Name] = null;
                else if ((Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType) == typeof(bool)) row[property.Name] = Convert.ToInt64(value) != 0;
                else if (value is byte[]) continue;
                else row[property.Name] = JsonSerializer.SerializeToNode(value);
            }
            var id = Convert.ToString(reader[key.GetColumnName()!], CultureInfo.InvariantCulture)!;
            // Synthetic media metadata has no editable database row in Images.
            if (table == "Images" && long.TryParse(id, out var number) && number >= 1000000000) continue;
            result[id] = row;
        }
        return result;
    }
    public async Task<JsonObject> Commit(AppDBContext db)
    {
        var changes = new JsonArray();
        foreach (var table in models.Keys.Where(table => table != "AuditLogs"))
        {
            var after = ReadRows(db, table);
            var before = initial[table];
            foreach (var id in before.Select(x => x.Key).Union(after.Select(x => x.Key)))
            {
                if (JsonNode.DeepEquals(before[id], after[id])) continue;
                var refs = new JsonObject();
                foreach (var foreign in models[table].GetForeignKeys().Where(f => f.Properties.Count == 1))
                    refs[foreign.Properties[0].Name] = foreign.PrincipalEntityType.GetTableName();
                changes.Add(new JsonObject {
                    ["table"] = table, ["key"] = id, ["recordId"] = int.TryParse(id, out var number) ? JsonValue.Create(number) : JsonValue.Create(id),
                    ["operation"] = before[id] is null ? "add" : after[id] is null ? "delete" : "update",
                    ["revision"] = Snapshot["revisions"]?[table]?[id]?.DeepClone() ?? JsonValue.Create(0),
                    ["values"] = after[id]?.DeepClone() ?? new JsonObject(), ["references"] = refs
                });
            }
        }
        if (changes.Count == 0) return new JsonObject();
        using var client = Client();
        using var response = await client.PostAsJsonAsync("api/web/commit", new JsonObject { ["changes"] = changes });
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"4D did not save the changes ({(int)response.StatusCode}): {body}");
        return JsonNode.Parse(body)!["ids"]!.AsObject();
    }
}
