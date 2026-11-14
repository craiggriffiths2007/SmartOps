namespace SmartOps.Shared.Models;

public static class SurveySpecificationReview
{
    public static string Validation(CustomItemBase item, DynamicQuestionDefinition definition)
    {
        string Value(string id) => item.GetSurveyAnswerValue(id);
        if (item.QuestionSetKey.Equals("Glass", StringComparison.OrdinalIgnoreCase)
            && Value("glass_order_status") == "Ready for supplier confirmation")
        {
            string warning = GlassQuestionValues.ThicknessWarning(item);
            if (!string.IsNullOrWhiteSpace(warning)) return warning + " Choose technical review required or correct the build-up.\n";
        }
        if (item.QuestionSetKey.Equals("ItemAssembly", StringComparison.OrdinalIgnoreCase)
            && definition.Questions.Any(q => q.Id == "escape_requirement")
            && Value("assembly_order_status") == "Ready for supplier confirmation" && Value("escape_requirement") == "Required")
        {
            double oldWidth = GlassQuestionValues.Number(item, "existing_clear_width"), oldHeight = GlassQuestionValues.Number(item, "existing_clear_height");
            double width = GlassQuestionValues.Number(item, "proposed_clear_width"), height = GlassQuestionValues.Number(item, "proposed_clear_height");
            if (oldWidth > 0 && oldHeight > 0 && width > 0 && height > 0 && (width < oldWidth || height < oldHeight))
                return "The proposed escape opening reduces an existing clear dimension. Choose technical review required or confirm a suitable configuration.\n";
        }
        return "";
    }
}
