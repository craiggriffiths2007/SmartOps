
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{
    public class Agent : TenantEntity
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [Display(Name = "Code")]
        public string Code { get; set; } = string.Empty;
        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; } = string.Empty;
        [Display(Name = "Serial Number")]
        [StringLength(100)]
        public string? SerialNumber { get; set; }

        [Display(Name = "Privileged Access (all jobs; own device required)")]
        public bool PrivilegedAccess { get; set; }

        [Display(Name = "Type")]
        public agent_type Type { get; set; }

        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Authentication String")]
        public string AuthenticationString { get; set; } = string.Empty;

        [Display(Name = "Last logged in")]
        public DateTime LoggedInDateTime { get; set; } = DateTime.Now;

        [Display(Name = "Current app version")]
        public string? CurrentAppVersion { get; set; }


        // Branch relationship
        [Display(Name = "Branch")]
        public int? BranchId { get; set; }

        public Branch? Branch { get; set; }

    }
}
