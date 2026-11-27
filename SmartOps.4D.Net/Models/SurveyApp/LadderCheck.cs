using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
namespace SmartOps.Web.Models
{
    public class LadderCheck : TenantEntity
    {
        [Browsable(false)]
        public int Id { get; set; }
        [Browsable(false)]
        [DisplayName("Unique Record ID (GUID)")]
        public Guid Guid { get; set; } = Guid.NewGuid();

        public string CheckID { get; set; } = ""; // consists of date, pda code, and unique Van Check ID number. Kind of a guid for the van check
        // --- Metadata & Inspection Details ---

        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;

        [DisplayName("Date Completed")]
        public string date_done { get; set; } = "";

        [DisplayName("Branch")]
        public string branch { get; set; } = "";

        [DisplayName("Ladder Number / Asset ID")]
        public string ladder_number { get; set; } = "";

        [DisplayName("Ladder Type")]
        public string ladder_type { get; set; } = "";

        [DisplayName("Vehicle Registration")]
        public string registration { get; set; } = "";

        [DisplayName("Fitter / Surveyor Name")]
        public string fitter_surveyor_name { get; set; } = "";

        [DisplayName("Manager's Name")]
        public string managers_name { get; set; } = "";


        // --- Ladder Condition Checklist ---

        [DisplayName("In Reasonable Overall Condition")]
        public string in_reasonable_condition { get; set; } = "";

        [DisplayName("Any Rungs Missing or Loose?")]
        public string rungs_missing_or_loose { get; set; } = "";

        [DisplayName("Any Stiles Damaged or Bent?")]
        public string stiles_damaged_or_bent { get; set; } = "";

        [DisplayName("Any Cracks Found?")]
        public string any_cracks { get; set; } = "";

        [DisplayName("Any Corrosion Found?")]
        public string any_corrosion { get; set; } = "";

        [DisplayName("Rubber/Plastic Feet Intact & Secure?")]
        public string rubber_plastic_feet { get; set; } = "";

        [DisplayName("Any Sharp Edges or Metal Splinters?")]
        public string sharp_or_metal_splinters { get; set; } = "";

        [DisplayName("Any Rungs Dented?")]
        public string rungs_dented { get; set; } = "";

        [DisplayName("Has Ladder Been Painted or Decorated?")]
        public string painted_or_decorated { get; set; } = "";

        [DisplayName("Do Hooks Sit & Secure Properly?")]
        public string hooks_sit_properly { get; set; } = "";

        [DisplayName("Has the Ladder Ever Been Repaired?")]
        public string ladders_been_repaired { get; set; } = "";

        [DisplayName("Any General Damage Detected?")]
        public bool? any_damage { get; set; }


        // --- Comments, Photos, & Workflow ---

        [DisplayName("Inspector Comments")]
        public string comments { get; set; } = "";

        [DisplayName("Total Photos Taken")]
        public int no_of_photos { get; set; }

        [DisplayName("Sent to Server")]
        public bool bSent { get; set; }

        [DisplayName("Is Completed")]
        public bool bComplete { get; set; }


        // --- Signatures ---

        [DisplayName("Inspector Signed")]
        public bool bSigned { get; set; }

        [DisplayName("Manager Signed")]
        public bool bSigned2 { get; set; }

        [DisplayName("Inspector Signature Filename")]
        public string signature_filename { get; set; } = "";

        [DisplayName("Manager Signature Filename")]
        public string signature_filename_2 { get; set; } = "";
    }


}
