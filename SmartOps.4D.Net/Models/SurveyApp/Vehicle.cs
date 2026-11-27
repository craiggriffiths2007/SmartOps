using System.ComponentModel;

using System.ComponentModel.DataAnnotations;



namespace SmartOps.Web.Models

{

    public class ChecksDTO
    {
        public string TenantCode { get; set; } = "";
        public string AgentCode { get; set; } = "";
        public string AuthenticationString { get; set; } = "";
        public List<LadderCheck> LadderChecks { get; set; } = new();
        public List<ToolCheck> ToolChecks { get; set; } = new();
        public List<SpotCheck> SpotChecks { get; set; } = new();
        public List<FAccident> FAccidents { get; set; } = new();
        public List<MileageSheet> MileageSheets { get; set; } = new();
        public List<AccidentsVehicleDTO> AccidentsVehicleDTOs { get; set; } = new();

        public List<VehicleCheckDTO> VehicleCheckDTOs { get; set; } = new();
    }

    public class JsonDTO
    {
        public string TenantCode { get; set; } = "";
        public string AgentCode { get; set; } = "";

        public string AuthenticationString { get; set; } = "";
        public string json { get; set; } = "";
    }

    public class Vehicle : TenantEntity
    {
        public int Id { get; set; }
        public int BranchId { get; set; } = 0;
        public string? Name { get; set; } = null;
        public string? Registration { get; set; } = null;
        public string? Description { get; set; } = null;
        public enum_vehicle_type Type { get; set; }

        public Branch? Branch { get; set; }
    }

    public class VehicleDTO
    {
        public int Id { get; set; }
        public int BranchId { get; set; } = 0;
        public string BranchCode { get; set; } = "";
        public string Name { get; set; } = "";
        public string Registration { get; set; } = "";
        public string Description { get; set; } = "";
        public enum_vehicle_type Type { get; set; }

        public VehicleDTO(Vehicle vehicle)
        {
            {
                Id = vehicle.Id;
                BranchId = vehicle.BranchId;
                BranchCode = vehicle.Branch.Code;
                Name = vehicle.Name;
                Registration = vehicle.Registration;
                Description = vehicle.Description;
                Type = vehicle.Type;
            }
        }
    }

    public class VehicleCheckDTO
    {
        public VehicleCheckHeader VehicleCheckHeader { get; set; } = new();
        public List<VehicleCheck> Vehicles { get; set; } = new();
    }
    public class VehicleCheckHeader : VehicleCheckHeaderBase { [Key] public int Id { get; set; } }
    public class VehicleCheck : VehicleCheckBase { [Key] [Browsable(false)] public int Id { get; set; } }
}
