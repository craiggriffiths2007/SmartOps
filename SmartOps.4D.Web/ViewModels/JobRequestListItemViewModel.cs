using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class JobRequestListItemViewModel
    {
        public int RequestId { get; set; }

        public int ClaimId { get; set; }

        public string Postcode { get; set; } = "";

        public DateTime IncidentDate { get; set; }

        public string CauseOfDamage { get; set; } = "";

        public DateTime RequestedDate { get; set; }

        public InsuranceClaimRequestStatus Status { get; set; }

        public int? ContractId { get; set; }

        public string ContractCode { get; set; } = "";
    }
}