using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOps.Web.Models
{
    public class Customer : TenantEntity
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Name")]
        public string Name { get; set; } = "";

        [Display(Name = "Address")]
        public string Add1 { get; set; } = "";
        [Display(Name = "Address 2")]
        public string? Add2 { get; set; } 
        [Display(Name = "Address 3")]
        public string? Add3 { get; set; }

        [Display(Name = "Postcode")]
        public string Postcode { get; set; } = "";

        [Display(Name = "Mobile")]
        public string Phone1 { get; set; } = "";
        [Display(Name = "Home")]
        public string? Phone2 { get; set; } 
        [Display(Name = "Work")]
        public string? Phone3 { get; set; }

        public string IssuedPassword { get; set; } = "";


        // Customer App login
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";

        public string AuthenticationString { get; set; } = "";
        public DateTime? LoggedInDateTime { get; set; }

        [DisplayName("Active")]
        public bool IsActive { get; set; } = true;
    }
}
