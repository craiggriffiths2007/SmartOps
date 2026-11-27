using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SmartOps.Web.Models
{
    public class UserFormViewModel
    {
        [Display(Name = "Branch")]
        public string? BranchCode { get; set; }
        public bool LockBranch { get; set; }

        public IEnumerable<SelectListItem> Branches { get; set; } = new List<SelectListItem>();

        public int? TenantId { get; set; }

        public IEnumerable<SelectListItem> Tenants { get; set; }
            = new List<SelectListItem>();

        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; } = "";

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "Profile Photo")]
        public IFormFile? Photo { get; set; }

        public List<ManageUserRolesViewModel> Roles { get; set; }
            = new();
    }

    public class CreateUserViewModel : UserFormViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        [StringLength(64, MinimumLength = 12)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

    }

    public class EditUserViewModel : UserFormViewModel
    {
        public string Id { get; set; } = "";

        [DataType(DataType.Password)]
        [StringLength(64, MinimumLength = 12)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        public byte[]? ExistingPhoto { get; set; }
    }
}