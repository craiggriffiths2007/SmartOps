using System.ComponentModel;
using System.Text.Json;

namespace SmartOps.Shared.Models
{
    public class ToolCheckBase : TenantEntity
    {
        // Dynamic tool questionnaire. The tool availability / fit-for-purpose
        // questions live in SurveyQuestions/ToolCheckQuestions.json.
        public string SurveyAnswersJson { get; set; } = "[]";

        public List<DynamicAnswer> GetSurveyAnswers()
        {
            if (string.IsNullOrWhiteSpace(SurveyAnswersJson)) return [];
            try { return JsonSerializer.Deserialize<List<DynamicAnswer>>(SurveyAnswersJson) ?? []; }
            catch { return []; }
        }

        public void SetSurveyAnswers(List<DynamicAnswer> answers)
            => SurveyAnswersJson = JsonSerializer.Serialize(answers);

        [Browsable(false)]
        [DisplayName("Unique Record ID (GUID)")]
        public Guid Guid { get; set; } = Guid.NewGuid();

        [Browsable(false)]
        [DisplayName("Check ID")]
        public string CheckID { get; set; } = "";

        public DateTime DateTimeCompleted { get; set; } = DateTime.Today;

        // LEGACY DYNAMIC FIELD: [DisplayName("Files (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? files_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Pliers (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? pliers_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Chisels (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? chisels_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Pincers (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? pincers_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Scraper (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? scraper_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Hacksaw (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? hacksaw_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Crowbar (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? crowbar_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Handsaw (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? handsaw_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Mole Grips (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? molegrips_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Side Cutters (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? sidecutters_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Hammer (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? hammer_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Spirit Level (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? spiritlevel_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Screwdrivers (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? screwdrivers_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Bolster Chisel (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? bolsterchisel_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Set Square (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? setsquare_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Stanley Knife (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? stanleyknife_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Club Hammer (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? clubhammer_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Tape Measure (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? tapemeasure_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Sliding Bevel (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? slidingbevel_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Glazing Shovel (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? glazingshovel_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Pointing Trowel (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? pointingtrowel_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Set of Allen Keys (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? setofallenkeys_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Adjustable Spanner (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? adjustablespanner_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Auger Bits (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? augerbits_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Nail Punch (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? nailpunch_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Putty Knife (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? puttyknife_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Socket Set (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? socketset_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Coping Saw (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? copingsaw_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Auger Bits - Joinery (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? augerbitsjoin_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Nail Punch - Joinery (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? nailpunchjoin_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Putty Knife - Joinery (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? puttyknifejoin_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Socket Set - Joinery (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? socketsetjoin_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Coping Saw - Joinery (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? copingsawjoin_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Rivet Gun - Joinery (Auditor)")]
        // LEGACY DYNAMIC FIELD: public bool? rivetgunjoin_a { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Files (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? files_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Pliers (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? pliers_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Chisels (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? chisels_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Pincers (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? pincers_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Scraper (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? scraper_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Hacksaw (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? hacksaw_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Crowbar (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? crowbar_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Handsaw (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? handsaw_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Mole Grips (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? molegrips_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Side Cutters (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? sidecutters_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Hammer (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? hammer_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Spirit Level (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? spiritlevel_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Screwdrivers (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? screwdrivers_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Bolster Chisel (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? bolsterchisel_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Set Square (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? setsquare_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Stanley Knife (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? stanleyknife_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Club Hammer (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? clubhammer_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Tape Measure (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? tapemeasure_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Sliding Bevel (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? slidingbevel_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Glazing Shovel (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? glazingshovel_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Pointing Trowel (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? pointingtrowel_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Set of Allen Keys (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? setofallenkeys_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Adjustable Spanner (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? adjustablespanner_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Auger Bits (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? augerbits_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Nail Punch (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? nailpunch_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Putty Knife (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? puttyknife_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Socket Set (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? socketset_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Coping Saw (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? copingsaw_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Auger Bits - Joinery (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? augerbitsjoin_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Nail Punch - Joinery (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? nailpunchjoin_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Putty Knife - Joinery (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? puttyknifejoin_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Socket Set - Joinery (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? socketsetjoin_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Coping Saw - Joinery (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? copingsawjoin_f { get; set; }
        // LEGACY DYNAMIC FIELD: [DisplayName("Rivet Gun - Joinery (Fitter)")]
        // LEGACY DYNAMIC FIELD: public bool? rivetgunjoin_f { get; set; }

        [DisplayName("Date Completed")]
        public string date_done { get; set; } = "";

        [Browsable(false)]
        [DisplayName("Is Completed")]
        public bool bComplete { get; set; }

        [Browsable(false)]
        [DisplayName("Sent to Server")]
        public bool bSent { get; set; }

        [Browsable(false)]
        [DisplayName("Fitter Signed")]
        public bool bSigned { get; set; }

        [Browsable(false)]
        [DisplayName("Auditor Signed")]
        public bool bSigned2 { get; set; }

        [Browsable(false)]
        [DisplayName("Fitter Signature Filename")]
        public string signature_filename { get; set; } = "";

        [Browsable(false)]
        [DisplayName("Auditor Signature Filename")]
        public string signature_filename2 { get; set; } = "";

        [Browsable(false)]
        [DisplayName("Fitter Printed Name")]
        public string signature_printed { get; set; } = "";

        [Browsable(false)]
        [DisplayName("Auditor Printed Name")]
        public string signature_printed2 { get; set; } = "";

        [DisplayName("Vehicle Registration")]
        public string registration { get; set; } = "";

        [DisplayName("Branch")]
        public string branch { get; set; } = "";

        [Browsable(false)]
        [DisplayName("Photo Filename")]
        public string photo_filename { get; set; } = "";
    }
}
