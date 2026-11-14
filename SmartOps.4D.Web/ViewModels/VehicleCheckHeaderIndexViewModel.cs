using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class VehicleCheckHeaderIndexViewModel
    {
        public VehicleCheckHeader? VehicleCheckHeader { get; set; }
        public IEnumerable<VehicleCheckVehicle>? VehicleCheckVehicles { get; set; }
    }
}
