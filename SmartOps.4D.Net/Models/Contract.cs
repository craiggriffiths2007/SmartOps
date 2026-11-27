using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOps.Web.Models
{

    public class ContractUpdate : TenantEntity
    {
        public int Id { get; set; }

        public int ContractId { get; set; }

        public Contract? Contract { get; set; }

        public DateTime DateTime { get; set; }

        public ContractStatus Status { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public bool CustomerVisible { get; set; }
    }

    public class Contract : TenantEntity
    {
        [Key]
        public int Id { get; set; }

        // Global sequential contract number
        public int ContractNumber { get; set; }

        [NotMapped]
        [DisplayName("Contract Code")]
        public string ContractCode => ContractNumber.ToString("D8");

        [DisplayName("Customer Reference")]
        public string CustomerReference { get; set; } = "";

        public ContractStatus Status { get; set; } = ContractStatus.Reported;

        [ConcurrencyCheck]
        public int? SelectedEstimateId { get; set; }

        [DisplayName("Traffic Light")]
        [EnumDataType(typeof(SmartOps.Shared.Enums.enum_tlight))]
        public SmartOps.Shared.Enums.enum_tlight TrafficLight { get; set; }
            = SmartOps.Shared.Enums.enum_tlight.Red;

        public int CustomerId { get; set; }


        public Customer? Customer { get; set; }

        // Map to SQL "date" (no time) and render as date-only in editors
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        [DisplayName("Incident Date")]
        public DateTime IncidentDate { get; set; } = DateTime.Today;
        [DisplayName("Damage Description")]
        [Required]
        public string DamageDescription { get; set; } = "";
        [Required]
        [DisplayName("Cause of Damage")]
        public string CauseOfDamage { get; set; } = "";
        [Required]

        [DisplayName("Excess")]
        public string Excess { get; set; } = "";
        [Required]
        [DisplayName("Policy Number")]
        public string PolicyNumber { get; set; } = "";

        [Display(Name = "Insurance Company")]
        public int? InsurerId { get; set; }

        public Insurer? Insurer { get; set; }

        public int? InsuranceClaimId { get; set; }

        public InsuranceClaim? InsuranceClaim { get; set; }

        public Tenant? Tenant { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsCancelled { get; set; } = false;

        public DateTime? CancelledAt { get; set; }

        public string? CancellationReason { get; set; }
    }
}
