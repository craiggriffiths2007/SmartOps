using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using SmartOps.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace SmartOps.Web.Services;

/// <summary>Exports mapped columns, with tenant scope applied explicitly to every table.</summary>
public sealed class TenantBackupService(AppDBContext db, IConfiguration configuration)
{
    public sealed record Table(string Name, IEntityType Entity, string Predicate);
    public sealed record MediaFile(string RelativePath, long Size);
    private readonly ISqlGenerationHelper sql = db.GetService<ISqlGenerationHelper>();
    private string Quote(string name) => sql.DelimitIdentifier(name);
    private string TableSql(IEntityType entity) => sql.DelimitIdentifier(entity.GetTableName()!, entity.GetSchema());
    private string Column(IEntityType entity, string property, string alias) =>
        $"{Quote(alias)}.{Quote(entity.FindProperty(property)!.GetColumnName(StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema()))!)}";

    public IReadOnlyList<Table> GetTables()
    {
        var entities = db.Model.GetEntityTypes().Where(e => e.GetTableName() != null).ToArray();
        var scopes = new Dictionary<IEntityType, string>();
        foreach (var entity in entities)
        {
            if (entity.FindProperty("TenantId") != null)
                scopes[entity] = $"{Column(entity, "TenantId", "row")} = @tenantId";
            else if (entity.ClrType == typeof(SmartOps.Web.Models.Tenant))
                scopes[entity] = $"{Column(entity, "Id", "row")} = @tenantId";
        }
        IEntityType Entity(string name) => entities.Single(e => e.ClrType.Name == name);
        string Match(IEntityType child, string childProperty, IEntityType parent, string parentProperty,
            string parentScope, string childAlias = "row", string parentAlias = "parent") =>
            $"EXISTS (SELECT 1 FROM {TableSql(parent)} AS {Quote(parentAlias)} WHERE "
            + $"{Column(child, childProperty, childAlias)} = {Column(parent, parentProperty, parentAlias)} AND "
            + $"({parentScope.Replace(Quote("row") + ".", Quote(parentAlias) + ".", StringComparison.Ordinal)}))";

        var claim = Entity("InsuranceClaim");
        var contract = Entity("Contract");
        var request = Entity("InsuranceClaimRequest");
        scopes[claim] = $"{Column(claim, "AssignedTenantId", "row")} = @tenantId OR "
            + Match(claim, "Id", contract, "InsuranceClaimId", scopes[contract], parentAlias: "claimContract") + " OR "
            + Match(claim, "Id", request, "InsuranceClaimId", scopes[request], parentAlias: "claimRequest");

        // Children without TenantId inherit ownership only from explicitly scoped parents.
        // Shared insurers are added afterwards, so they cannot pull in unrelated claims/users.
        foreach (var name in new[] { "GarageDoorMotor", "SecuritySurvey", "IdentityUserClaim`1",
            "IdentityUserLogin`1", "IdentityUserToken`1", "IdentityUserRole`1", "InsuranceClaimPhoto", "InsuranceClaimUpdate" })
        {
            var entity = Entity(name);
            var fk = entity.GetForeignKeys().Single(f => scopes.ContainsKey(f.PrincipalEntityType));
            scopes[entity] = Match(entity, fk.Properties.Single().Name, fk.PrincipalEntityType,
                fk.PrincipalKey.Properties.Single().Name, scopes[fk.PrincipalEntityType], parentAlias: "ownedParent");
        }
        var role = Entity("IdentityRole");
        var userRole = Entity("IdentityUserRole`1");
        scopes[role] = Match(role, "Id", userRole, "RoleId", scopes[userRole], parentAlias: "tenantUserRole");
        var roleClaim = Entity("IdentityRoleClaim`1");
        scopes[roleClaim] = Match(roleClaim, "RoleId", role, "Id", scopes[role], parentAlias: "tenantRole");

        var insurer = Entity("Insurer");
        var user = Entity("AppUser");
        scopes[insurer] = Match(insurer, "Id", contract, "InsurerId", scopes[contract], parentAlias: "insurerContract") + " OR "
            + Match(insurer, "Id", claim, "InsurerId", scopes[claim], parentAlias: "insurerClaim") + " OR "
            + Match(insurer, "Id", user, "InsurerId", scopes[user], parentAlias: "insurerUser");

        var appRequest = Entity("SurveyAppRequest");
        var tenant = entities.Single(e => e.ClrType == typeof(SmartOps.Web.Models.Tenant));
        scopes[appRequest] = Match(appRequest, "TenantCode", tenant, "Code", scopes[tenant], parentAlias: "requestTenant");

        return scopes.Select(pair => new Table(pair.Key.GetTableName()!, pair.Key, pair.Value))
            .OrderBy(table => table.Name, StringComparer.Ordinal).ToArray();
    }

    public async Task WriteTableAsync(Stream destination, Table table, int tenantId, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(destination, ReadRowsAsync(table, tenantId, cancellationToken),
            new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    private async IAsyncEnumerable<Dictionary<string, object?>> ReadRowsAsync(Table table, int tenantId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var store = StoreObjectIdentifier.Table(table.Entity.GetTableName()!, table.Entity.GetSchema());
        var columns = table.Entity.GetProperties().Select(p => p.GetColumnName(store)!).Distinct().ToArray();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT {string.Join(", ", columns.Select(c => Quote("row") + "." + Quote(c)))} "
                + $"FROM {TableSql(table.Entity)} AS {Quote("row")} WHERE ({table.Predicate})";
            command.CommandTimeout = 120;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenantId";
            parameter.DbType = DbType.Int32;
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < columns.Length; i++)
                    row[columns[i]] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                yield return row;
            }
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    private string MediaRoot(int tenantId) => Path.GetFullPath(Path.Combine(
        configuration["ImageStoragePath"] ?? @"C:\SmartOpsImages", tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    public IReadOnlyList<MediaFile> GetMediaFiles(int tenantId)
    {
        string root = MediaRoot(tenantId);
        if (!Directory.Exists(root)) return [];
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The tenant media folder cannot be a linked folder.");
        var options = new EnumerationOptions { RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false };
        return Directory.EnumerateFiles(root, "*", options)
            .Select(path => new MediaFile(Path.GetRelativePath(root, path).Replace('\\', '/'), new FileInfo(path).Length))
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
    }

    public string? ResolveMediaFile(int tenantId, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)
            || relativePath.Contains('\\') || relativePath.Contains(':')) return null;
        string[] segments = relativePath.Split('/');
        if (segments.Any(segment => segment is "" or "." or "..")) return null;
        string path = MediaRoot(tenantId);
        if (!Directory.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
        foreach (string segment in segments)
        {
            path = Path.Combine(path, segment);
            if (!Path.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
        }
        return File.Exists(path) ? path : null;
    }
}
