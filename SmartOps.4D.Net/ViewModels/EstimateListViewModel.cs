using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels;

public class EstimateListViewModel
{
    public int ContractId { get; set; }
    public int? SelectedEstimateId { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsInsurer { get; set; }
    public IReadOnlyList<Estimate> Estimates { get; set; } = Array.Empty<Estimate>();
}
