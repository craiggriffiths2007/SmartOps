using SmartOps.Web.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{


    public class Insurer
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(100)]
        public string ContactName { get; set; } = "";

        [MaxLength(100)]
        public string Email { get; set; } = "";

        [MaxLength(30)]
        public string Telephone { get; set; } = "";

        [MaxLength(150)]
        public string Address1 { get; set; } = "";

        [MaxLength(150)]
        public string? Address2 { get; set; }

        [MaxLength(150)]
        public string? Address3 { get; set; }

        [MaxLength(20)]
        public string Postcode { get; set; } = "";

        public bool IsActive { get; set; } = true;

        public ICollection<Contract> Contracts { get; set; }
            = new List<Contract>();
    }

    public enum InsurerMessageSender
    {
        Insurer,
        Company
    }

    public class InsurerMessage : TenantEntity
    {
        public int Id { get; set; }

        public int ContractId { get; set; }
        public Contract Contract { get; set; } = null!;

        public int InsurerId { get; set; }
        public Insurer Insurer { get; set; } = null!;

        public string Message { get; set; } = "";

        public DateTime Created { get; set; } = DateTime.UtcNow;

        public InsurerMessageSender Sender { get; set; }

        public string? SentByUserId { get; set; }
        public AppUser? SentByUser { get; set; }

        public bool ReadByInsurer { get; set; }
        public bool ReadByCompany { get; set; }
    }

    public class InsuranceClaim
    {
        public int Id { get; set; }

        public int InsurerId { get; set; }
        public Insurer Insurer { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string ClaimReference { get; set; } = "";

        [Required]
        [MaxLength(100)]
        public string CustomerName { get; set; } = "";

        [MaxLength(100)]
        public string Address1 { get; set; } = "";

        [MaxLength(100)]
        public string Address2 { get; set; } = "";

        [MaxLength(100)]
        public string Address3 { get; set; } = "";

        [MaxLength(20)]
        public string Postcode { get; set; } = "";

        [MaxLength(30)]
        public string Telephone { get; set; } = "";

        [MaxLength(100)]
        public string Email { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime IncidentDate { get; set; } = DateTime.Today;

        public string CauseOfDamage { get; set; } = "";

        public string DamageDescription { get; set; } = "";

        public string PolicyNumber { get; set; } = "";

        public decimal Excess { get; set; }

        public InsuranceClaimStatus Status { get; set; }
            = InsuranceClaimStatus.New;

        // Null until assigned to a glazing company
        public int? AssignedTenantId { get; set; }

        public DateTime Created { get; set; } = DateTime.UtcNow;

        public DateTime? AssignedDate { get; set; }

        public ICollection<InsuranceClaimPhoto> Photos { get; set; } = new List<InsuranceClaimPhoto>();
    }

    public class InsuranceClaimPhoto
    {
        public int Id { get; set; }

        public int InsuranceClaimId { get; set; }

        public InsuranceClaim InsuranceClaim { get; set; } = null!;

        public string Filename { get; set; } = "";

        public string OriginalFilename { get; set; } = "";

        public DateTime UploadedDate { get; set; } = DateTime.Now;
    }

    public enum InsuranceClaimStatus
    {
        New,
        OnJobBoard,
        Assigned,
        SurveyBooked,
        AwaitingApproval,
        RepairBooked,
        InProgress,
        Completed,
        Cancelled
    }

    public class InsuranceClaimRequest
    {
        public int Id { get; set; }

        public int InsuranceClaimId { get; set; }
        public InsuranceClaim InsuranceClaim { get; set; } = null!;

        public int TenantId { get; set; }

        public DateTime RequestedDate { get; set; } = DateTime.UtcNow;

        public InsuranceClaimRequestStatus Status { get; set; }
            = InsuranceClaimRequestStatus.Pending;
    }

    public enum InsuranceClaimRequestStatus
    {
        Pending,
        Accepted,
        Rejected,
        Withdrawn
    }


    public class InsuranceClaimUpdate
    {
        public int Id { get; set; }

        public int InsuranceClaimId { get; set; }
        public InsuranceClaim InsuranceClaim { get; set; } = null!;

        public DateTime DateTime { get; set; } = DateTime.UtcNow;

        public InsuranceClaimStatus Status { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public string? UserId { get; set; }
    }



}
