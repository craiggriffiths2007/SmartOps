using Microsoft.AspNetCore.Mvc.Rendering;

namespace SmartOps.Web.ViewModels;

public class VehicleChecksIndexViewModel
{
    public IReadOnlyList<VehicleCheckCardViewModel> Cards { get; set; } = [];
    public List<SelectListItem> Branches { get; set; } = [];
    public List<SelectListItem> CheckWeeks { get; set; } = [];
    public string? BranchCode { get; set; }
    public string? CheckWeek { get; set; }
}
