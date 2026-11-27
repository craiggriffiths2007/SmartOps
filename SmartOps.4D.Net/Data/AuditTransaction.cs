using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace SmartOps.Web.Data;

// Include the audit insert in an existing caller transaction's savepoint as well.
// An audit failure must not leave an unaudited mutation for that caller to commit.
internal sealed class AuditTransaction(IDbContextTransaction transaction, bool owns, string? savepoint)
    : IDisposable, IAsyncDisposable
{
    private bool completed;

    public static AuditTransaction Begin(DatabaseFacade db)
    {
        var current = db.CurrentTransaction;
        if (current is null) return new(db.BeginTransaction(), true, null);
        string? point = current.SupportsSavepoints ? "audit_" + Guid.NewGuid().ToString("N")[..26] : null;
        if (point is not null) current.CreateSavepoint(point);
        return new(current, false, point);
    }

    public static async Task<AuditTransaction> BeginAsync(DatabaseFacade db, CancellationToken token = default)
    {
        var current = db.CurrentTransaction;
        if (current is null) return new(await db.BeginTransactionAsync(token), true, null);
        string? point = current.SupportsSavepoints ? "audit_" + Guid.NewGuid().ToString("N")[..26] : null;
        if (point is not null) await current.CreateSavepointAsync(point, token);
        return new(current, false, point);
    }

    public void Commit()
    {
        if (owns) transaction.Commit();
        else if (savepoint is not null) transaction.ReleaseSavepoint(savepoint);
        completed = true;
    }

    public async Task CommitAsync(CancellationToken token = default)
    {
        if (owns) await transaction.CommitAsync(token);
        else if (savepoint is not null) await transaction.ReleaseSavepointAsync(savepoint, token);
        completed = true;
    }

    public void Dispose()
    {
        if (owns) transaction.Dispose();
        else if (!completed)
        {
            if (savepoint is not null) transaction.RollbackToSavepoint(savepoint);
            else transaction.Rollback();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (owns) await transaction.DisposeAsync();
        else if (!completed)
        {
            if (savepoint is not null) await transaction.RollbackToSavepointAsync(savepoint, CancellationToken.None);
            else await transaction.RollbackAsync(CancellationToken.None);
        }
    }
}
