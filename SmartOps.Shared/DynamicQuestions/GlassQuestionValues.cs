using System.Globalization;
using System.Text.RegularExpressions;
using SmartOps.Shared.Enums;

namespace SmartOps.Shared.Models;

/// <summary>Reads dynamic glass specifications for drawings, warnings and parent context.</summary>
public static class GlassQuestionValues
{
    public static string Value(CustomItemBase item, string id) =>
        item.GetSurveyAnswers().FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

    public static bool Yes(CustomItemBase item, string id) =>
        Value(item, id).Trim().ToLowerInvariant() is "yes" or "true";

    public static double Number(CustomItemBase item, string id) => ParseNumber(Value(item, id));

    private static double ParseNumber(string value)
    {
        if ((double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number))
            && double.IsFinite(number)) return number;
        return 0;
    }

    // ProductInto is parent identity for assembly sub-items; it is never a mirror
    // of standalone questionnaire answers.
    public static string Material(CustomItemBase item) => item.base_item is enum_item_type.custom
        ? item.ProductInto : Value(item, "ProductInto");

    public static bool SteppedAllowed(CustomItemBase item) => item.base_item == enum_item_type.none
        || item.base_item is (enum_item_type.custom) && Material(item).Equals("Timber", StringComparison.OrdinalIgnoreCase);

    public static string ThicknessWarning(CustomItemBase item)
    {
        string glazing = Value(item, "single_or_double");
        if (!string.IsNullOrWhiteSpace(Value(item, "pane1_thickness")))
        {
            int panes = glazing switch { "Single glazed" => 1, "Double glazed" => 2, "Triple glazed" => 3, _ => 0 };
            if (panes == 0) return ""; // Encapsulated units require the supplier's full build-up.
            double total = 0;
            for (int pane = 1; pane <= panes; pane++)
            {
                double thickness = Number(item, $"pane{pane}_thickness");
                if (thickness <= 0) return "";
                total += thickness;
                if (pane == panes) continue;
                double cavity = Number(item, $"cavity{pane}_width");
                if (cavity <= 0) return "";
                total += cavity;
            }
            double measured = Number(item, "overall_thickness");
            if (measured <= 0 || Math.Abs(total - measured) <= 0.05) return "";
            return string.Format(CultureInfo.InvariantCulture,
                "The proposed pane and cavity build-up totals {0:0.##}mm, but overall thickness is {1:0.##}mm. Confirm the specification with the supplier.", total, measured);
        }
        if (string.IsNullOrWhiteSpace(glazing) || glazing.Equals("Single glazed", StringComparison.OrdinalIgnoreCase)) return "";
        string spacer = Value(item, "spacer_thickness");
        int multiplier = spacer.StartsWith("2x", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        spacer = Regex.Replace(spacer, @"^2x\s*", "", RegexOptions.IgnoreCase);
        double spacerSize = ParseNumber(Regex.Match(spacer, @"^\d+(?:\.\d+)?").Value) * multiplier;
        double overall = Number(item, "overall_thickness");
        string glass = Value(item, "glass_type");
        double glassSize = glass switch
        {
            "4mm Tough + 6.4mm Lami" => 10.4,
            "6mm Tough + 6.4mm Lami" => 12.4,
            _ => ParseNumber(Regex.Match(glass, @"^\d+(?:\.\d+)?").Value)
                * (glazing.Equals("Triple glazed", StringComparison.OrdinalIgnoreCase) ? 3 : 2)
        };
        if (spacerSize <= 0 || overall <= 0 || glassSize <= 0 || Math.Abs(spacerSize + glassSize - overall) <= 0.05) return "";
        return string.Format(CultureInfo.InvariantCulture,
            "The spacer thickness does not match the overall thickness less the glass thickness.\nGlass thickness: {0:0.#}mm\nOverall thickness: {1:0.#}mm\nThe spacer should be: {2:0.#}mm",
            glassSize, overall, overall - glassSize);
    }

    public static string DesignValidation(CustomItemBase item, DynamicQuestionDefinition definition)
    {
        var answers = item.GetSurveyAnswers();
        var context = DynamicQuestionSnapshots.Context(item);
        var errors = new List<string>();
        string[] designInputs = ["special_glass", "lead_spacing_width", "lead_spacing_height", "lead_thickness",
            "lead_sod", "lead_type", "bar_thickness", "lead_anti_rattle"];
        foreach (var question in definition.Questions.Where(q => designInputs.Contains(q.Id) && q.Required))
        {
            if (!DynamicQuestionLogic.IsApplicable(question, answers, context)) continue;
            string value = Value(item, question.Id);
            bool valid = question.Type == DynamicQuestionType.PlusMinus ? PlusMinusQuestions.IsValid(question, value)
                : question.Type == DynamicQuestionType.Measurement
                ? DynamicQuestionLogic.IsValidMeasurement(question, value)
                : !string.IsNullOrWhiteSpace(value) && DynamicQuestionLogic.ResolveOptions(question, answers, context)
                    .Contains(value, StringComparer.OrdinalIgnoreCase);
            if (!valid) errors.Add(question.Question);
        }
        double width = Number(item, "unit1_width"), height = Number(item, "unit1_height");
        double spacingWidth = Number(item, "lead_spacing_width"), spacingHeight = Number(item, "lead_spacing_height");
        if (width <= 0) errors.Add("Glass width");
        if (height <= 0) errors.Add("Glass height");
        if (spacingWidth <= 0) errors.Add("Width spacing");
        if (spacingHeight <= 0) errors.Add("Height spacing");
        if (width > 0 && spacingWidth > width) errors.Add("Width spacing must fit within the glass width");
        if (height > 0 && spacingHeight > height) errors.Add("Height spacing must fit within the glass height");
        return string.Join("\n", errors.Distinct());
    }
}
