using System.Globalization;
using System.Text.Json;
using SmartOps.Web.Models;
using SmartOps.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace SmartOps.Web.Data;

public partial class AppDBContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    private void ConfigureAuditLog(ModelBuilder builder)
    {
        builder.Entity<AuditLog>(b => {
            b.HasIndex(x => new { x.TenantId, x.OccurredUtc, x.Id });
            b.HasIndex(x => new { x.OccurredUtc, x.Id });
            b.HasIndex(x => x.CorrelationId);
            b.HasQueryFilter(x => CurrentTenantId != null && x.TenantId == CurrentTenantId);
        });
    }

    private sealed record AuditChange(EntityEntry Entry, AuditLog Log);

    private List<AuditChange> CaptureAuditChanges()
    {
        ChangeTracker.DetectChanges();
        var result = new List<AuditChange>();
        foreach (var entry in ChangeTracker.Entries().Where(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
        {
            // Telemetry is already a record, and Identity token/login keys can contain credentials.
            string type = entry.Metadata.ClrType.Name;
            if (entry.Entity is PageHit or SurveyAppRequest || type.StartsWith("IdentityUserToken")
                || type.StartsWith("IdentityUserLogin")) continue;
            var log = AuditIdentity.Create(_auditHttp?.HttpContext, _tenantContext);
            log.Action = entry.State switch {
                EntityState.Added => "Created", EntityState.Deleted => "Deleted", _ => "Updated"
            };
            log.EntityType = AuditIdentity.Limit(type, 128);
            var tenantProperty = entry.Metadata.FindProperty("TenantId");
            // The affected record's company wins, especially for a superuser or insurer.
            log.TenantId = entry.Entity is Tenant company ? company.Id == 0 ? null : company.Id
                : tenantProperty is not null ? (int?)(entry.State == EntityState.Deleted
                    ? entry.Property("TenantId").OriginalValue : entry.Property("TenantId").CurrentValue)
                : log.TenantId;
            if (entry.Entity is AuditLog editedLog)
            {
                log.Action = "Log note edited";
                log.TenantId = editedLog.TenantId;
                log.CompanyName = editedLog.CompanyName;
            }
            else if (entry.Entity is Tenant t) log.CompanyName = AuditIdentity.Limit(t.Name, 200);
            else if (log.TenantId != CurrentTenantId) log.CompanyName = null;
            // Field names only: never persist passwords, business data, images or answer JSON.
            log.ChangedFields = AuditIdentity.Limit(string.Join(", ", entry.Properties
                .Where(p => entry.State != EntityState.Modified || p.IsModified)
                .Select(p => p.Metadata.Name).OrderBy(x => x)), 4000);
            result.Add(new(entry, log));
        }
        return result;
    }

    private void FinishAuditChange(AuditChange change)
    {
        var key = change.Entry.Metadata.FindPrimaryKey();
        change.Log.EntityId = AuditIdentity.Limit(JsonSerializer.Serialize(key?.Properties.ToDictionary(
            p => p.Name, p => Convert.ToString(change.Entry.Property(p.Name).CurrentValue, CultureInfo.InvariantCulture))), 512);
        if (change.Entry.Entity is Tenant tenant) change.Log.TenantId = tenant.Id;
    }

    // Insert independently of the change tracker, in the SAME connection/transaction as the mutation.
    // This preserves generated keys and the SaveChanges(false) contract without recursive saves.
    public int WriteAuditEvent(AuditLog e) => Database.ExecuteSqlInterpolated($@"
        INSERT INTO [AuditLogs] ([OccurredUtc], [TenantId], [CompanyName], [UserId], [UserName],
        [Action], [EntityType], [EntityId], [ChangedFields], [Method], [Route], [CorrelationId], [StatusCode], [Notes], [Revision])
        VALUES ({e.OccurredUtc}, {e.TenantId}, {e.CompanyName}, {e.UserId}, {e.UserName},
        {e.Action}, {e.EntityType}, {e.EntityId}, {e.ChangedFields}, {e.Method}, {e.Route}, {e.CorrelationId}, {e.StatusCode}, {e.Notes}, {e.Revision})");

    public Task<int> WriteAuditEventAsync(AuditLog e, CancellationToken token = default) => Database.ExecuteSqlInterpolatedAsync($@"
        INSERT INTO [AuditLogs] ([OccurredUtc], [TenantId], [CompanyName], [UserId], [UserName],
        [Action], [EntityType], [EntityId], [ChangedFields], [Method], [Route], [CorrelationId], [StatusCode], [Notes], [Revision])
        VALUES ({e.OccurredUtc}, {e.TenantId}, {e.CompanyName}, {e.UserId}, {e.UserName},
        {e.Action}, {e.EntityType}, {e.EntityId}, {e.ChangedFields}, {e.Method}, {e.Route}, {e.CorrelationId}, {e.StatusCode}, {e.Notes}, {e.Revision})", token);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (BridgeImporting) return base.SaveChanges(acceptAllChangesOnSuccess);
        SetTenantIds();
        var changes = CaptureAuditChanges();
        if (changes.Count == 0) return base.SaveChanges(acceptAllChangesOnSuccess);
        int Save()
        {
            using var transaction = AuditTransaction.Begin(Database);
            int count = base.SaveChanges(false);
            foreach (var change in changes) { FinishAuditChange(change); WriteAuditEvent(change.Log); }
            transaction.Commit();
            return count;
        }
        int result = Database.CurrentTransaction is null ? Database.CreateExecutionStrategy().Execute(Save) : Save();
        if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
        return result;
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken token = default)
    {
        if (BridgeImporting) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, token);
        SetTenantIds();
        var changes = CaptureAuditChanges();
        if (changes.Count == 0) return await base.SaveChangesAsync(acceptAllChangesOnSuccess, token);
        async Task<int> Save()
        {
            await using var transaction = await AuditTransaction.BeginAsync(Database, token);
            int count = await base.SaveChangesAsync(false, token);
            foreach (var change in changes) { FinishAuditChange(change); await WriteAuditEventAsync(change.Log, token); }
            await transaction.CommitAsync(token);
            return count;
        }
        int result = Database.CurrentTransaction is null ? await Database.CreateExecutionStrategy().ExecuteAsync(Save) : await Save();
        if (acceptAllChangesOnSuccess) ChangeTracker.AcceptAllChanges();
        return result;
    }
}
