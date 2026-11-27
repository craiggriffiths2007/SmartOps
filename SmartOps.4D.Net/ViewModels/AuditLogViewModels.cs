using System.ComponentModel.DataAnnotations;
using SmartOps.Web.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SmartOps.Web.ViewModels;

public sealed class AuditLogFilter
{
    public int? TenantId { get; set; }
    public bool DatabaseUpdatesOnly { get; set; } = true;
    [StringLength(460)] public string? Actor { get; set; }
    [StringLength(32)] public string? EventAction { get; set; }
    [StringLength(128)] public string? EntityType { get; set; }
    [StringLength(200)] public string? Search { get; set; }
    [DataType(DataType.Date)] public DateTime? From { get; set; }
    [DataType(DataType.Date)] public DateTime? To { get; set; }
    [RegularExpression("^(success|failure)?$")] public string? Outcome { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class AuditLogIndexViewModel
{
    public AuditLogFilter Filter { get; set; } = new();
    public bool IsSuperAdmin { get; set; }
    public List<AuditLog> Entries { get; set; } = [];
    public List<SelectListItem> Companies { get; set; } = [];
    public List<SelectListItem> Users { get; set; } = [];
    public List<SelectListItem> Actions { get; set; } = [];
    public List<SelectListItem> RecordTypes { get; set; } = [];
    public int Total { get; set; }
    public int PageCount => Math.Max(1, (int)Math.Ceiling(Total / 50d));
}

public sealed class AuditLogEditViewModel
{
    public long Id { get; set; }
    public Guid Revision { get; set; }
    [StringLength(4000)] public string? Notes { get; set; } = "";
    public AuditLog? Entry { get; set; }
}
