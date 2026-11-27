using System;
using System.Threading.Tasks;

namespace SmartOps.Web.Data;

// Compatibility definitions retained for legacy Identity and tenant-management pages.
// The 4D server owns all demonstration records and seeds them on first startup.
// These old SQLite-based tenant registration data generators are deliberately
// unavailable in the bridge application: writing into the disposable request
// database would not persist new records in 4D.
public enum Roles
{
    Admin,
    Users,
    Diary,
    Branch,
    SuperAdmin,
    Insurer
}

public static class DbInitializer
{
    public static Task AddTestDataAsync(AppDBContext context, int tenantId)
        => throw new NotSupportedException(
            "The 4D-backed SmartOps demo is seeded automatically in 4D. " +
            "Legacy ASP.NET tenant data seeding is not supported.");

    public static Task AddSmallTestDataAsync(AppDBContext context, int tenantId)
        => throw new NotSupportedException(
            "The 4D-backed SmartOps demo is seeded automatically in 4D. " +
            "Legacy ASP.NET tenant data seeding is not supported.");
}
