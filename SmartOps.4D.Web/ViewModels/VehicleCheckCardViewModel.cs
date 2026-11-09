using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels;

public class VehicleCheckCardViewModel
{
    public VehicleCheckHeader Header { get; set; } = new();
    public Dictionary<enum_vehicle_type, List<VehicleCheckRegistrationViewModel>> Registrations { get; set; } = new();
}

public class VehicleCheckRegistrationViewModel
{
    public string Registration { get; set; } = "";
    public bool IsIncomplete { get; set; }
}
