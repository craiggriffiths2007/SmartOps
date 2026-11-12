using SmartOps.Web.Models;

namespace SmartOps.Web.Areas.Insurer.ViewModels
{
    public class InsuranceClaimListItemViewModel
    {
        public int Id { get; set; }

        public string ClaimReference { get; set; } = "";

        public string CustomerName { get; set; } = "";

        public string Postcode { get; set; } = "";

        public DateTime IncidentDate { get; set; }

        public InsuranceClaimStatus Status { get; set; }

        public DateTime Created { get; set; }

        public string AssignedTenantName { get; set; } = "";

        public int? ContractId { get; set; }

        public string ContractCode { get; set; } = "";

        public int BidCount { get; set; }
    }
}