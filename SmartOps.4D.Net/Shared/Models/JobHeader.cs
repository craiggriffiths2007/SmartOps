using SmartOps.Shared.Enums;
using System.ComponentModel;

namespace SmartOps.Shared.Models
{
    public class JobHeaderBase : TenantEntity
    {
        [Browsable(false)]
        public Guid Guid { get; set; } = Guid.NewGuid();

        [DisplayName("Job Type")]
        public enum_job_type JobType { get; set; }

        [DisplayName("Agent Code")]
        public string AgentCode { get; set; } = "";

        [Browsable(false)]
        public int ContractId { get; set; }

        [Browsable(false)]
        public int CustomerId { get; set; }

        [Browsable(false)]
        public int? BranchId { get; set; }

        [Browsable(false)]
        public int AgentId { get; set; }

        [DisplayName("Complete")]
        [Browsable(false)]
        public bool bComplete { get; set; }

        [DisplayName("Sent")]
        [Browsable(false)]
        public bool bSent { get; set; }

        #region Scheduling & Diary Details

        [DisplayName("Diary Date")]
        public DateTime DiaryDate { get; set; } = DateTime.Today;

        [DisplayName("Start Time")]
        public string StartTime { get; set; } = "";

        [DisplayName("Finish Time")]
        public string FinishTime { get; set; } = "";

        [DisplayName("En Route Date and Time (UTC)")]
        public DateTime? EnRoutePressedAtUtc { get; set; }

        [DisplayName("Surveyor Time Arrived")]
        public string TimeArrived { get; set; } = "00:00";

        [DisplayName("Completed DateTime")]
        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;

        #endregion

        #region Customer & Contract Information

        [Browsable(false)]
        [DisplayName("Contract Code")]
        public string ContractCode { get; set; } = "";

        [DisplayName("Policy Number")]
        public string PolicyNumber { get; set; } = "";

        [DisplayName("Invoice Number")]
        public string InvoiceNumber { get; set; } = "";

        [DisplayName("Insurance Company Name")]
        public string InsuranceCompanyName { get; set; } = "";

        [DisplayName("Loss Adjuster Name")]
        public string LossAdjusterName { get; set; } = "";

        [DisplayName("Incident Date")]
        public string IncidentDate { get; set; } = "";

        [DisplayName("COD String")]
        public string CauseOfDamage { get; set; } = "";

        [DisplayName("Damage Description")]
        public string DamageDescription { get; set; } = "";

        [DisplayName("Name")]
        public string CustomerName { get; set; } = "";

        [DisplayName("Address 1")]
        public string CustomerAddressLine1 { get; set; } = "";

        [DisplayName("Address 2")]
        public string CustomerAddressLine2 { get; set; } = "";

        [DisplayName("Address 3")]
        public string CustomerAddressLine3 { get; set; } = "";

        [DisplayName("Address 4")]
        public string CustomerAddressLine4 { get; set; } = "";

        [DisplayName("Postcode")]
        public string CustomerPostcode { get; set; } = "";

        [DisplayName("Phone Number")]
        public string CustomerPhoneNumber { get; set; } = "";

        [DisplayName("Phone Number 2")]
        public string CustomerPhoneNumber2 { get; set; } = "";

        [DisplayName("Phone Number 3")]
        public string CustomerPhoneNumber3 { get; set; } = "";

        [DisplayName("Additional Phone 1")]
        public string CustomerAddPhoneNumber1 { get; set; } = "";

        [DisplayName("Additional Phone 2")]
        public string CustomerAddPhoneNumber2 { get; set; } = "";

        [DisplayName("Traffic Light")]
        public enum_tlight TrafficLight { get; set; }

        #endregion

        #region Job, Instructions & Progress

        [Browsable(false)]
        [DisplayName("Instructions")]
        public string JobInstructions { get; set; } = "";

        [DisplayName("Number of Items")]
        public int NumberOfItems { get; set; }

        [Browsable(false)]
        [DisplayName("Report Text")]
        public string Report { get; set; } = "";

        #endregion

        [Browsable(false)] public string HealthAndSafetyAnswersJson { get; set; } = "";
        [Browsable(false)] public string HealthAndSafetyQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? HealthAndSafetyQuestionSetVersion { get; set; }

        [Browsable(false)] public string SpecialRequirementsAnswersJson { get; set; } = "";
        [Browsable(false)] public string SpecialRequirementsQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? SpecialRequirementsQuestionSetVersion { get; set; }

        [Browsable(false)] public string JobSummaryAnswersJson { get; set; } = "";
        [Browsable(false)] public string JobSummaryQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? JobSummaryQuestionSetVersion { get; set; }
        [Browsable(false)] public string RepudiationAnswersJson { get; set; } = "";
        [Browsable(false)] public string RepudiationQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? RepudiationQuestionSetVersion { get; set; }
        [Browsable(false)] public string GarageDoorMotorDeclarationAnswersJson { get; set; } = "";
        [Browsable(false)] public string GarageDoorMotorDeclarationQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? GarageDoorMotorDeclarationQuestionSetVersion { get; set; }

        [Browsable(false)] public string FitterCompletionAnswersJson { get; set; } = "";
        [Browsable(false)] public string FitterCompletionQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? FitterCompletionQuestionSetVersion { get; set; }
        [Browsable(false)] public string FitterStockUsageAnswersJson { get; set; } = "";
        [Browsable(false)] public string FitterStockUsageQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? FitterStockUsageQuestionSetVersion { get; set; }
        [Browsable(false)] public string RemedialCompletionAnswersJson { get; set; } = "";
        [Browsable(false)] public string RemedialCompletionQuestionsSnapshotJson { get; set; } = "";
        [Browsable(false)] public int? RemedialCompletionQuestionSetVersion { get; set; }

        #region Fitting Safety & Site Attendance

        [DisplayName("Front House Photos")]
        public int no_of_front_house_photos { get; set; }

        #endregion

        #region Fitting

        [Browsable(false)]
        [DisplayName("Fitter Comments")]
        public string FitComments { get; set; } = "";

        [DisplayName("Time Arrived")]
        public string FitTimeArrived { get; set; } = "";

        [DisplayName("Time Left")]
        public string FitTimeLeft { get; set; } = "";

        [DisplayName("Fitting Complete")]
        public bool bFitterJobComplete { get; set; } = true;

        public bool bFitterCompletionFormComplete { get; set; }

        [DisplayName("Fitter Signature Date")]
        public string f_sign_date { get; set; } = "";

        [DisplayName("Directive Complete")]
        public int directive_complete { get; set; }

        [DisplayName("Number of Photos")]
        public int no_of_fitting_photos { get; set; }

        #endregion

        #region Mandate & Job Completion

        [DisplayName("Additional Image Complete")]
        public bool bFitterAdditionalImage { get; set; }

        [DisplayName("Fitter Mandate Signed")]
        public bool? bFitterMandateSigned { get; set; }

        [Browsable(false)]
        [DisplayName("Mandate Sentence")]
        public string MandateSentence { get; set; } = "";

        [Browsable(false)]
        public DateTime MandateSignedDate { get; set; } = DateTime.Today;

        [DisplayName("Other cause of damage")]
        public string OtherCauseOfDamage { get; set; } = "";

        [DisplayName("Completion Signed")]
        public bool bFitterSigned { get; set; }

        [DisplayName("Fitter Stock Usage Complete")]
        public bool bFitterStockUsageComplete { get; set; }

        #endregion

        #region Financials & Payments

        [DisplayName("Account Code")]
        public string si_mpay { get; set; } = "";

        [DisplayName("Excess Amount")]
        public string Excess { get; set; } = "";

        [DisplayName("Survey")]
        public bool bSurvey { get; set; }

        [DisplayName("Survey Complete")]
        public bool? survey_complete { get; set; } = true;

        #endregion

        #region Remedial Action Details

        [DisplayName("Remedial Additional Image")]
        public bool readditimage { get; set; }

        [DisplayName("Remedial Completion Form Complete")]
        public bool bRemedialCompletionFormComplete { get; set; }

        [DisplayName("Remedial Comments")]
        public string RemedialReport { get; set; } = "";

        [DisplayName("Remedial Completion Form Complete")]
        public bool bRemedialCustomerSigned { get; set; }

        [DisplayName("Remedial Additional Image")]
        public bool bRemedialAdditionalImage { get; set; }

        #endregion

        #region Item Numbering

        [DisplayName("Current Item Number")]
        [Browsable(false)]
        public int current_item_number { get; set; }

        #endregion

        #region Repudiation

        [DisplayName("Request Repudiation")]
        public bool bRequestRepudiation { get; set; } = false;

        #endregion

        #region Fitting Equipment & Motor

        [DisplayName("Garage Door Motor")]
        public bool? garage_door_motor { get; set; }

        #endregion

        [DisplayName("Globaldoor Link")]
        public string global_door_link { get; set; } = "";

        [DisplayName("Lintel Signed")]
        public bool bFitterLintelSigned { get; set; }

        [DisplayName("Number of Videos")]
        public int no_of_videos { get; set; }
    }

    public class GarageDoorMotorBase
    {
        public bool MotorSigned { get; set; }
        public bool MotorSignedCustomer { get; set; }
    }
    public class SecuritySurveyBase
    {
        public bool? bIsSecuritySurvey { get; set; }
        public int bIsComplete { get; set; }

        public string nowindows { get; set; } = "";
        public string nodoors { get; set; } = "";
        public string gencondition { get; set; } = "";
        public string gencondition_other { get; set; } = "";
        public string matwindows { get; set; } = "";
        public string matwindows_other { get; set; } = "";
        public string matdoors { get; set; } = "";
        public string matdoors_other { get; set; } = "";
        public string lockwindows { get; set; } = "";
        public string lockwindows_other { get; set; } = "";
        public string lockdoors { get; set; } = "";
        public string lockdoors_other { get; set; } = "";

        public bool? add_window_security { get; set; }
        public string location_windows_other { get; set; } = "";
        public string secwindows_other { get; set; } = "";

        public bool? add_door_security { get; set; }
        public string location_doors_other { get; set; } = "";
        public string secdoors_other { get; set; } = "";

        public string time_required { get; set; } = "";
        public int no_of_photos { get; set; }
    }

}
