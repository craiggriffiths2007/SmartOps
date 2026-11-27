using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class InsurerEditViewModel
    {
        public Insurer Insurer { get; set; } = new();

        public List<InsurerUserViewModel> Users { get; set; } = new();

        public string NewFirstName { get; set; } = "";
        public string NewLastName { get; set; } = "";
        public string NewEmail { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    public class InsurerUserViewModel
    {
        public string Id { get; set; } = "";

        public string FirstName { get; set; } = "";

        public string LastName { get; set; } = "";

        public int? InsurerId { get; set; }

        public string Email { get; set; } = "";

        public string FullName => $"{FirstName} {LastName}".Trim();

        public bool IsDisabled { get; set; }

        public string? NewPassword { get; set; } = "";
    }
}