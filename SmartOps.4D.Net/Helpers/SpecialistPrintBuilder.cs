using System.Globalization;
using SmartOps.Shared.Models;
using SmartOps.Web.ViewModels;

namespace SmartOps.Web.Helpers;

public static class SpecialistPrintBuilder
{
    private static readonly string[] Types = ["Deadlock", "Hook", "Latch", "Cam", "Mushroom"];
    private static readonly string[] Patterns = ["Diamond Leaded", "Georgian Leaded", "Georgian Bar", "Back to Back Spacer"];
    public static bool CanPrintLock(CustomItemBase item) => item.QuestionSetKey.Equals("Lockmech", StringComparison.OrdinalIgnoreCase)
        && (GlassQuestionValues.Yes(item, "bMulti") || string.IsNullOrWhiteSpace(GlassQuestionValues.Value(item, "bMulti"))
            && (item.l_num > 0 || item.bLockComplete));
    public static bool CanPrintLeading(CustomItemBase item) => item.QuestionSetKey.Equals("Glass", StringComparison.OrdinalIgnoreCase)
        && Patterns.Contains(GlassQuestionValues.Value(item, "special_glass"));
    public static bool IsMeasurement(string? value, bool allowZero = false) => TryNumber(value, out double n) && (allowZero ? n >= 0 : n > 0);
    private static bool TryNumber(string? value, out double n) =>
        (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out n)
        || double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("en-GB"), out n)
        || double.TryParse(value, NumberStyles.Float, CultureInfo.GetCultureInfo("fr-FR"), out n)) && double.IsFinite(n);
    private static string Recorded(string? value) => string.IsNullOrWhiteSpace(value) ? "Not recorded" : value.Trim();
    private static string Mm(string? value, bool allowZero = false) => IsMeasurement(value, allowZero) ? value!.Trim() + " mm" : "Not recorded";

    public static SpecialistPrintViewModel Build(CustomItemBase item, bool isLock)
    {
        var specs = new List<SpecialistPrintField>();
        var snapshot = DynamicQuestionSnapshots.Read(item);
        void Answer(string id, string fallback, bool measurement = false)
        {
            string label = snapshot?.Questions.FirstOrDefault(q => q.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Question ?? fallback;
            string value = GlassQuestionValues.Value(item, id);
            specs.Add(new(label, measurement ? Mm(value) : Recorded(value)));
        }
        var measurements = new List<SpecialistPrintField>();
        var components = new List<string>();
        var bolts = new List<SpecialistPrintField>();
        bool complete;
        string? warning = null;
        if (isLock)
        {
            string kind = GlassQuestionValues.Value(item, "item");
            bool window = kind.Equals("Window", StringComparison.OrdinalIgnoreCase);
            Answer("item", "Door / window"); Answer("locking_make", "Lock make");
            Answer("locking_codes", "Lock codes"); Answer("COD_Code", "Supplier / brochure");
            if (window) Answer("GearBox", "Gearbox");
            string[] types = [item.l_type1, item.l_type2, item.l_type3, item.l_type4, item.l_type5, item.l_type6, item.l_type7];
            // Legacy window records may count invisible end markers. They aren't components.
            components.AddRange(types.Take(Math.Clamp(item.l_num, 0, 7)).Where(t => Types.Contains(t)));
            measurements.Add(new("1 · Distance 1", Mm(item.l_size1)));
            if (!window) measurements.Add(new("2 · Distance 2", Mm(item.l_size2)));
            string[] spans = [item.l_sizeA, item.l_sizeB, item.l_sizeC, item.l_sizeD, item.l_sizeE, item.l_sizeF, item.l_sizeG];
            int count = window ? components.Count + 1 : Math.Max(0, components.Count - 1);
            for (int i = 0; i < Math.Min(count, 7); i++)
                measurements.Add(new($"{(char)('A' + i)} · Span", Mm(spans[i])));
            if (window)
            {
                bool recorded = item.l_num > 0 || item.bLockComplete;
                bolts.Add(new("Top shoot bolt", recorded ? item.left_bolt == 1 ? "Fitted" : "Not fitted" : "Not recorded"));
                bolts.Add(new("Bottom shoot bolt", recorded ? item.right_bolt == 1 ? "Fitted" : "Not fitted" : "Not recorded"));
            }
            complete = item.bLockComplete && (window || kind.Equals("Door", StringComparison.OrdinalIgnoreCase))
                && components.Count is > 0 and <= 6 && measurements.All(m => m.Value != "Not recorded");
            if (types.Take(Math.Clamp(item.l_num, 0, 7)).Any(t => !string.IsNullOrWhiteSpace(t) && t != "dt_none" && !Types.Contains(t)) || item.l_num > 7 || item.l_num < 0)
            {
                complete = false;
                warning = "Some saved component details could not be identified. Check the survey before ordering.";
            }
        }
        else
        {
            string pattern = GlassQuestionValues.Value(item, "special_glass");
            Answer("special_glass", "Pattern");
            Answer("unit1_width", "Glass width", true); Answer("unit1_height", "Glass height", true);
            Answer("lead_spacing_width", "Width spacing", true); Answer("lead_spacing_height", "Height spacing", true);
            if (pattern is "Diamond Leaded" or "Georgian Leaded")
            {
                Answer("lead_thickness", "Lead thickness"); Answer("lead_type", "Lead finish"); Answer("lead_sod", "Lead single or double");
            }
            if (pattern == "Georgian Bar") { Answer("bar_thickness", "Bar thickness / colour"); Answer("lead_anti_rattle", "Anti rattle"); }
            if (pattern == "Back to Back Spacer") { Answer("back_to_back_spacer_width", "Spacer depth"); Answer("back_to_back_spacer_height", "Overall spacer width"); }
            Answer("gb_trim", "Trim 30 mm");
            measurements.AddRange(new[] { new SpecialistPrintField("A · Bottom", Mm(item.sizeA, true)),
                new("B · Left", Mm(item.sizeB, true)), new("C · Right", Mm(item.sizeC, true)), new("D · Top", Mm(item.sizeD, true)) });
            complete = item.bSpecialGlassComplete && measurements.All(m => m.Value != "Not recorded")
                && new[] { "unit1_width", "unit1_height", "lead_spacing_width", "lead_spacing_height" }.All(id => IsMeasurement(GlassQuestionValues.Value(item, id)));
            if (GlassQuestionValues.Number(item, "lead_spacing_width") > GlassQuestionValues.Number(item, "unit1_width")
                || GlassQuestionValues.Number(item, "lead_spacing_height") > GlassQuestionValues.Number(item, "unit1_height")) complete = false;
        }
        return new SpecialistPrintViewModel { Item = item, IsLock = isLock, IsComplete = complete,
            Specifications = specs, Measurements = measurements, Components = components, Bolts = bolts, Warning = warning };
    }
}
