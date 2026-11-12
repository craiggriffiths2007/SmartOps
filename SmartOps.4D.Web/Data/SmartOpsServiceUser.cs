using Microsoft.AspNetCore.Identity;
using SmartOps.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace SmartOps.Web.Data
{
    // Add profile data for application users by adding properties to the PropertyRepairsUser class
    public class AppUser : IdentityUser
    {
        public string? BranchCode { get; set; }
        public bool LockBranch { get; set; }

        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }

        public int? InsurerId { get; set; }
        public Insurer? Insurer { get; set; }

        public bool IsApproved { get; set; } = false;

        public DateTime? ApprovedAt { get; set; }

        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        public int UsernameChangeLimit { get; set; } = 10;
        public byte[]? ProfilePicture { get; set; } = Array.Empty<byte>();

        public DateTime? ProfilePictureUpdated { get; set; }
    }

}
