namespace SmartOps.Web.Models
{
    public class UserRolesViewModel
    {
        public string? BranchCode { get; set; }
        public bool LockBranch { get; set; }
        public string? UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public int? TenantId { get; set; }
        public string? Tenant { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public IEnumerable<string>? Roles { get; set; }
        public byte[]? ProfilePicture { get; set; }
    }
}
