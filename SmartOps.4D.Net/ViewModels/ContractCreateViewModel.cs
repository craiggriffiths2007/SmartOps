using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class ContractCreateViewModel
    {
        // Customer

        public string CustomerName { get; set; } = "";

        public string Address1 { get; set; } = "";

        public string Address2 { get; set; } = "";

        public string Address3 { get; set; } = "";

        public string Postcode { get; set; } = "";

        public string Telephone { get; set; } = "";

        public string Email { get; set; } = "";

        // Contract

        public ContractStatus Status { get; set; }
            = ContractStatus.Reported;

        public DateTime IncidentDate { get; set; }
            = DateTime.Today;

        public string DamageDescription { get; set; } = "";

        public string CauseOfDamage { get; set; } = "";

        public string Excess { get; set; } = "";

        public string PolicyNumber { get; set; } = "";

        public int? InsurerId { get; set; }
    }
}
