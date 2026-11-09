using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Areas.Insurer.ViewModels
{
    public class InsuranceClaimCreateViewModel
    {
        [Required]
        [Display(Name = "Claim Reference")]
        public string ClaimReference { get; set; } = "";

        [Required]
        [Display(Name = "Customer Name")]
        public string CustomerName { get; set; } = "";

        [Required]
        [Display(Name = "Address 1")]
        public string Address1 { get; set; } = "";

        public string? Address2 { get; set; }

        public string? Address3 { get; set; }

        [Required]
        public string Postcode { get; set; } = "";

        public string? Telephone { get; set; }

        public string? Email { get; set; }

        [DataType(DataType.Date)]
        public DateTime IncidentDate { get; set; } = DateTime.Today;

        [Required]
        [Display(Name = "Cause of Damage")]
        public string CauseOfDamage { get; set; } = "";

        [Display(Name = "Damage Description")]
        public string? DamageDescription { get; set; }

        [Display(Name = "Policy Number")]
        public string? PolicyNumber { get; set; }

        public decimal Excess { get; set; }

        public int? AssignedTenantId { get; set; }

        public bool AddToJobBoard { get; set; }

        public List<IFormFile> Photos { get; set; } = new();
    }
}
