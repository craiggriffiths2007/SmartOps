using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models;

// No foreign keys: history survives removal of users, records and companies.
public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredUtc { get; set; }
    public int? TenantId { get; set; }
    [MaxLength(200)] public string? CompanyName { get; set; }
    [MaxLength(450)] public string? UserId { get; set; }
    [MaxLength(256)] public string UserName { get; set; } = "System";
    [MaxLength(32)] public string Action { get; set; } = "";
    [MaxLength(128)] public string EntityType { get; set; } = "";
    [MaxLength(512)] public string EntityId { get; set; } = "";
    [MaxLength(4000)] public string ChangedFields { get; set; } = "";
    [MaxLength(16)] public string Method { get; set; } = "";
    [MaxLength(512)] public string Route { get; set; } = "";
    [MaxLength(128)] public string CorrelationId { get; set; } = "";
    public int? StatusCode { get; set; }
    [MaxLength(4000)] public string Notes { get; set; } = "";
    // Application-managed concurrency token, also supported by SQLite checks.
    [ConcurrencyCheck] public Guid Revision { get; set; } = Guid.NewGuid();
}
