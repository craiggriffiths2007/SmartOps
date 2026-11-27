
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartOps.Web.Models
{
    public class Job : TenantEntity
    {
        public int Id { get; set; }
        public int ContractId { get; set; }

        public int CustomerId { get; set; }

        public int AgentId { get; set; }

        [Display(Name = "Branch")]
        public int? BranchId { get; set; }
        public Branch? Branch { get; set; }

        public bool EnRouteNotificationSent { get; set; }
        public DateTime? EnRoutePressedAtUtc { get; set; }

        [Display(Name = "Contract Code")]
        public string ContractCode { get; set; } = "";

        [Display(Name = "Job Type")]
        public enum_job_type JobType { get; set; } = enum_job_type.Survey;

        [Display(Name = "Diary Date")]
        [DataType(DataType.Date)]

        public DateTime DiaryDate { get; set; } = DateTime.Today;


        [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
        [Display(Name = "Start Time")]
        [DataType(DataType.Time)]
        public DateTime Time { get; set; } = DateTime.Now;

        [DisplayFormat(DataFormatString = @"{0:hh\:mm}", ApplyFormatInEditMode = true)]
        [Display(Name = "Time allocated")]
        [DataType(DataType.Time)]
        public TimeSpan JobTime { get; set; } = TimeSpan.FromHours(1);


        [Display(Name = "Incident Date")]
        public DateTime IncidentDate { get; set; } = DateTime.Today;

        [Display(Name = "Insurance Company Name")]
        public string InsuranceCompanyName { get; set; } = "";
        [Display(Name = "Excess")]
        public string Excess { get; set; } = "";

        [Display(Name = "Policy Number")]
        public string PolicyNumber { get; set; } = "";

        [Display(Name = "Cause of damage")]
        public string CauseOfDamage { get; set; } = "";

        [Display(Name = "Damage")]
        public string DamageDesc { get; set; }
        [Display(Name = "Instructions")]
        public string Instructions { get; set; }

        [Display(Name = "Request Repudiation")]
        public bool bRequestRepudiation { get; set; } = false;
        [Display(Name = "Include Survey")]
        public bool bIncludeSurvey { get; set; } = true;


        public Agent? Agent { get; set; }

        public Customer? Customer { get; set; }

        public Contract? Contract { get; set; }
        /*
        public Job()
        {
            Date = DateTime.Today;
            Time = DateTime.Now;
        }*/
    }

    public class GetDataDTO
    {
        public string TenantCode { get; set; } = "";
        public string AgentCode { get; set; } = "";

        public string AuthenticationString { get; set; } = "";

        public string Password { get; set; } = "";

        public string Filename { get; set; } = "";

        public string BranchCode { get; set; } = "";

        public string ContractCode { get; set; } = ""; 

        public string Note { get; set; } = ""; 
    }

    public class AgentLoginDTO
    {
        public string TenantCode { get; set; } = "";
        public string AgentCode { get; set; } = "";
        public string Password { get; set; } = "";
        public string AuthenticationString { get; set; } = "";
        public string CurrentVersion { get; set; } = "";
        public string CurrentBuild { get; set; } = "";

        public string? BranchCode { get; set; }

        public agent_type AgentType { get; set; }
    }


    public class JobContractDTO
    {
        public int Id { get; set; }
        public int ContractId { get; set; }
        public enum_job_type JobType { get; set; } = enum_job_type.Survey;
        public string ContractCode { get; set; } = "";
        public DateTime DiaryDate { get; set; } = DateTime.Today;
        public string Time { get; set; } = "";
        public string EndTime { get; set; } = "";
        public string Name { get; set; } = "";
        public string Add1 { get; set; } = "";
        public string Add2 { get; set; } = "";
        public string Add3 { get; set; } = "";
        public string Postcode { get; set; } = "";
        public string Phone1 { get; set; } = "";
        public string Phone2 { get; set; } = "";
        public string Phone3 { get; set; } = "";

        public string IncidentDate { get; set; } = "";
        public string CauseOfDamage { get; set; } = "";
        public string DamageDesc { get; set; } = "";
        public string Instructions { get; set; } = "";

        public string Excess { get; set; } = "";
        public string PolicyNumber { get; set; } = "";

        [DisplayName("Insurance Company")]
        public string InsuranceCompanyName { get; set; } = "";
        public bool bRequestRepudiation { get; set; } = false;
        public List<ContractNote> ContractNotes { get; set; } = new List<ContractNote>();

        public JobContractDTO() { }
        public JobContractDTO(Job jobItem,Customer custItem)
        { 
            

            (Id, ContractId, ContractCode, DiaryDate, Time, EndTime, Name, Add1, Add2, Add3, Postcode, Phone1, Phone2, Phone3, DamageDesc, Instructions, CauseOfDamage, IncidentDate, bRequestRepudiation, InsuranceCompanyName, JobType, Excess, PolicyNumber) =

            (jobItem.Id, jobItem.ContractId, jobItem.ContractCode, jobItem.DiaryDate, jobItem.Time.ToShortTimeString(), jobItem.Time.AddHours(1).ToShortTimeString(), custItem.Name,
                custItem.Add1, custItem.Add2, custItem.Add3, custItem.Postcode, custItem.Phone1,
                custItem.Phone2, custItem.Phone3, jobItem.DamageDesc, jobItem.Instructions, jobItem.CauseOfDamage, jobItem.IncidentDate.ToShortDateString(),jobItem.bRequestRepudiation, jobItem.InsuranceCompanyName, jobItem.JobType, jobItem.Excess, jobItem.PolicyNumber);

        }

        public JobContractDTO(Contract contractItem, Customer custItem)
        {

            (Id, ContractId, ContractCode, Name, Add1, Add2, Add3, Postcode, Phone1, Phone2, Phone3, DamageDesc,CauseOfDamage, IncidentDate,  InsuranceCompanyName, Excess, PolicyNumber) =

            (contractItem.Id, contractItem.Id, contractItem.ContractCode, custItem.Name,
                custItem.Add1, custItem.Add2, custItem.Add3, custItem.Postcode, custItem.Phone1,
                custItem.Phone2, custItem.Phone3, contractItem.DamageDescription, contractItem.CauseOfDamage, contractItem.IncidentDate.ToShortDateString(), contractItem.Insurer?.Name ?? "", contractItem.Excess, contractItem.PolicyNumber);

        }
    }

}
