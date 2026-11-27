using SmartOps.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Data;

public partial class AppDBContext
{
    // ExecuteUpdate/Delete bypass SaveChanges. Keep the operation and its event in one transaction.
    public async Task<int> AuditBulkAsync(string entityType, int? tenantId, string action, Func<Task<int>> operation)
    {
        async Task<int> Execute()
        {
            await using var transaction = await AuditTransaction.BeginAsync(Database);
            var entry = AuditIdentity.Create(_auditHttp?.HttpContext, _tenantContext);
            entry.TenantId = tenantId;
            entry.CompanyName = tenantId.HasValue ? AuditIdentity.Limit(await Tenant.IgnoreQueryFilters()
                .Where(t => t.Id == tenantId).Select(t => t.Name).FirstOrDefaultAsync(), 200) : null;
            int count = await operation();
            if (count > 0)
            {
                entry.Action = action;
                entry.EntityType = AuditIdentity.Limit(entityType, 128);
                entry.EntityId = $"{count} affected records";
                await WriteAuditEventAsync(entry);
            }
            await transaction.CommitAsync();
            return count;
        }
        return Database.CurrentTransaction is null
            ? await Database.CreateExecutionStrategy().ExecuteAsync(Execute) : await Execute();
    }
}
