using SmartOps.Shared.Enums;
using System.ComponentModel;
using System.Text.Json;

namespace SmartOps.Shared.Models
{
    public class VehicleCheckHeaderBase : TenantEntity
    {
        public Guid Guid { get; set; } = Guid.NewGuid();
        public string CheckWeekDate { get; set; } = "";
        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;
        public string BranchCode { get; set; } = "";  // branch code
        public string CheckID { get; set; } = ""; // consists of date, pda code, and unique Van Check ID number. Kind of a guid for the van check

        public bool bComplete { get; set; }
        public bool bSent { get; set; }
        public int TotalDeliveryHGVs { get; set; }
        public int TotalDeliveryVans { get; set; }
        public int TotalFitterVans { get; set; }
        public int TotalSalesCars { get; set; }
        public int CompleteDeliveryHGVs { get; set; }
        public int CompleteDeliveryVans { get; set; }
        public int CompleteFitterVans { get; set; }
        public int CompleteSalesCars { get; set; }

    }

    public class VehicleCheckBase : TenantEntity
    {
        // enum_vehicle_type stored as an int so Shared remains UI-agnostic.
        public int VehicleType { get; set; }
        // Dynamic vehicle questionnaire. Ordinary check questions now live in
        // SmartOps.Shared/SurveyQuestions/*Questions.json.
        public string SurveyAnswersJson { get; set; } = "[]";

        public List<DynamicAnswer> GetSurveyAnswers()
        {
            if (string.IsNullOrWhiteSpace(SurveyAnswersJson)) return [];
            try { return JsonSerializer.Deserialize<List<DynamicAnswer>>(SurveyAnswersJson) ?? []; }
            catch { return []; }
        }

        public void SetSurveyAnswers(List<DynamicAnswer> answers)
            => SurveyAnswersJson = JsonSerializer.Serialize(answers);

        // --- System & Metadata Fields ---

        [Browsable(false)]
        [DisplayName("Unique Record ID (GUID)")]
        public Guid Guid { get; set; } = Guid.NewGuid();

        [Browsable(false)]
        [DisplayName("Vehicle Check Header ID")]
        public int VehicleCheckHeaderId { get; set; }

        [Browsable(false)]
        [DisplayName("Check ID")]
        public string CheckID { get; set; }

        [Browsable(false)]
        [DisplayName("Item Number")]
        public int item_number { get; set; } = 0;

        [DisplayName("Destination")]
        public string Destination { get; set; } = "";

        [DisplayName("Check Completed Date/Time")]
        public string CheckDoneDate { get; set; } = "";

        [DisplayName("Vehicle Registration")]
        public string Registration { get; set; } = "";

        [DisplayName("Current Mileage")]
        public string Mileage { get; set; } = "";

        [DisplayName("Branch Code")]
        public string BranchCode { get; set; } = "";

        [DisplayName("PDA Application Version Date")]
        public string PDAVersionDate { get; set; } = "";

        // --- Check Status & Workflow ---

        [DisplayName("Is Complete Status (Value)")]
        public bool? is_complete { get; set; } // relates to reason not complete question

        [DisplayName("Reason Not Completed")]
        public string ReasonNotCompleted { get; set; } = "";

        [DisplayName("Is Completed")]
        public bool bComplete { get; set; }

        [DisplayName("Sent to Server")]
        public bool bSent { get; set; }

        [DisplayName("External Photos Taken")]
        public bool bPhotosOutsideComplete { get; set; }

        [DisplayName("Internal Photos Taken")]
        public bool bPhotosInsideComplete { get; set; }

        [DisplayName("Damage Diagrams Completed")]
        public bool bDiagramsComplete { get; set; }

        [DisplayName("Signatures Completed")]
        public bool bSignaturesComplete { get; set; }

        // --- Tyre Pressures ---

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Passenger Front")]
        // LEGACY DYNAMIC FIELD: public bool? pressure_passenger_front { get; set; }

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Passenger Rear")]
        // LEGACY DYNAMIC FIELD: public bool? pressure_passenger_rear { get; set; }

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Driver Front")]
        // LEGACY DYNAMIC FIELD: public bool? pressure_driver_front { get; set; }

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Driver Rear")]
        // LEGACY DYNAMIC FIELD: public bool? pressure_driver_rear { get; set; }

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Spare")]
        // LEGACY DYNAMIC FIELD: public bool? pressure_spare { get; set; }

        // --- Tyre Pressure Notes / Serial Numbers ---

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Passenger Front (Notes)")]
        // LEGACY DYNAMIC FIELD: public string pressure_passenger_front_s { get; set; } = "";

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Passenger Rear (Notes)")]
        // LEGACY DYNAMIC FIELD: public string pressure_passenger_rear_s { get; set; } = "";

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Driver Front (Notes)")]
        // LEGACY DYNAMIC FIELD: public string pressure_driver_front_s { get; set; } = "";

        // LEGACY DYNAMIC FIELD: [DisplayName("Tyre Pressure - Driver Rear (Notes)")]
        // LEGACY DYNAMIC FIELD: public string pressure_driver_rear_s { get; set; } = "";

        [DisplayName("Tyre Pressure - Spare (Notes)")]
        public string pressure_spare_s { get; set; } = "";

        // --- Vehicle Damage Descriptions ---

        [DisplayName("Damage Description - Passenger Side")]
        public string damage_pass { get; set; } = "";

        [DisplayName("Damage Description - Driver Side")]
        public string damage_driver { get; set; } = "";

        [DisplayName("Damage Description - Front")]
        public string damage_front { get; set; } = "";

        [DisplayName("Damage Description - Rear")]
        public string damage_back { get; set; } = "";

        // --- Photo Verification Counts ---

        [DisplayName("Front Photos Count")]
        public bool bPhotoFront { get; set; }

        [DisplayName("Rear Photos Count")]
        public bool bPhotoRear { get; set; }

        [DisplayName("Left Side Photos Count")]
        public bool bPhotoLeft { get; set; }

        [DisplayName("Right Side Photos Count")]
        public bool bPhotoRight { get; set; }

        // --- Signatures & Verification ---

        [DisplayName("Driver Signed")]
        public bool bDriverSigned { get; set; }

        [DisplayName("Checked By Signed")]
        public bool bCheckedBySigned { get; set; }

        [DisplayName("Driver Printed Name")]
        public string driver_printed { get; set; } = "";

        [DisplayName("Auditor/Checker Printed Name")]
        public string checked_printed { get; set; } = "";
    }

}
