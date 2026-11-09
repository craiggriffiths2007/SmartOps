using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace SmartOps.Web.Models
{
    public class SpotCheck : TenantEntity
    {
        public string SpotCheckAnswersJson { get; set; } = "";
        public string SpotCheckQuestionsSnapshotJson { get; set; } = "";
        public int? SpotCheckQuestionSetVersion { get; set; }

        [Browsable(false)]
        public int Id { get; set; }
        [Browsable(false)]
        [DisplayName("GUID")]
        public Guid Guid { get; set; } = Guid.NewGuid();
        [Browsable(false)]
        [DisplayName("Check ID")]
        public string CheckID { get; set; } = ""; // consists of date, pda code, and unique Van Check ID number. Kind of a guid for the van check

        [DisplayName("Date Completed")]
        public string date_done { get; set; } = string.Empty;

        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;
        [Browsable(false)]
        [DisplayName("Complete")]
        public bool bComplete { get; set; }
        [Browsable(false)]
        [DisplayName("Sent")]
        public bool bSent { get; set; }
        [Browsable(false)]

        [DisplayName("Correct Contract")]
        public bool bCorrectContract { get; set; }
        [Display(Order = 0)]
        [DisplayName("Contract")]
        public string ContractCode { get; set; } = string.Empty;
        [Display(Order = 0)]
        [DisplayName("Branch")]
        public string branch { get; set; } = string.Empty;
        [Display(Order = 0)]
        [Column(TypeName = "date")]
        [DataType(DataType.Date)]
        [DisplayName("Diary Date")]
        public DateTime DiaryDate { get; set; } = DateTime.Today;
        [Display(Order = 0)]
        [DisplayName("Time")]
        public string Time { get; set; } = "";
        [Display(Order = 1)]
        [DisplayName("Name")]
        public string Name { get; set; } = "";
        [Display(Order = 1)]
        [DisplayName("Address 1")]
        public string Add1 { get; set; } = "";
        [Display(Order = 1)]
        [DisplayName("Address 2")]
        public string Add2 { get; set; } = "";
        [Display(Order = 1)]
        [DisplayName("Address 3")]
        public string Add3 { get; set; } = ""; 
        [Display(Order = 1)]
        [DisplayName("Postcode")]
        public string Postcode { get; set; } = "";
        [DisplayName("Phone 1")]
        public string Phone1 { get; set; } = "";
        [DisplayName("Phone 2")]
        public string Phone2 { get; set; } = "";
        [DisplayName("Phone 3")]
        public string Phone3 { get; set; } = "";
        [DisplayName("Damage")]
        public string DamageDesc { get; set; } = "";
        [Browsable(false)]
        [DisplayName("Instructions")]
        public string Instructions { get; set; } = "";


        [DisplayName("Photos")]
        public int no_of_photos { get; set; }

        public bool uspot_signed { get; set; }
        public string uspot_signeddate { get; set; } = string.Empty;
    }
}
