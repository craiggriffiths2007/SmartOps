using SmartOps.Shared.Models;

namespace SmartOps.Web.ViewModels;

public sealed record SpecialistPrintField(string Label, string Value);
public sealed class SpecialistPrintViewModel
{
    public required CustomItemBase Item { get; init; }
    public bool IsLock { get; init; }
    public bool IsComplete { get; init; }
    public string Title => IsLock ? "Lock mechanism details" : "Leading and bar details";
    public string CompanyName { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string PropertyAddress { get; set; } = "";
    public int ItemId { get; set; }
    public string? ImageFilename { get; set; }
    public List<SpecialistPrintField> Specifications { get; init; } = [];
    public List<SpecialistPrintField> Measurements { get; init; } = [];
    public List<string> Components { get; init; } = [];
    public List<SpecialistPrintField> Bolts { get; init; } = [];
    public string? Warning { get; init; }
}
