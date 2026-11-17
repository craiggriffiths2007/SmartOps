namespace SmartOps.Shared.Models;

/// <summary>
/// Stable, allow-listed widget/action identifiers. Definitions select behaviour;
/// renderers must explicitly register native handlers for device-only actions.
/// Unknown IDs must never be executed or treated as navigation targets.
/// </summary>
public static class DynamicQuestionWidgets
{
    public const string Default = "";
    public const string RatingFive = "RatingFive";
    public const string RatingTen = "RatingTen";
    public const string RatingGuide = "RatingGuide";
    public const string TimePicker = "TimePicker";
    public const string CapturePhotos = "CapturePhotos";
    public const string CaptureSignature = "CaptureSignature";
    public const string CaptureDrawing = "CaptureDrawing";
    public const string MotorDeclaration = "MotorDeclaration";

    public static IReadOnlySet<string> Allowed { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            RatingFive, RatingTen, RatingGuide, TimePicker,
            CapturePhotos, CaptureSignature, CaptureDrawing, MotorDeclaration
        };

    public static bool Supports(string widget, DynamicQuestionType type) =>
        (Allowed.FirstOrDefault(key => key.Equals(widget, StringComparison.OrdinalIgnoreCase)) ?? widget) switch
    {
        RatingFive or RatingTen =>
            type is DynamicQuestionType.Measurement or DynamicQuestionType.ButtonPicker,
        RatingGuide or CapturePhotos =>
            type == DynamicQuestionType.Information,
        CaptureSignature => type is DynamicQuestionType.Information or DynamicQuestionType.Text,
        CaptureDrawing or MotorDeclaration or TimePicker => type == DynamicQuestionType.Text,
        _ => string.IsNullOrWhiteSpace(widget)
    };

    public static string For(DynamicQuestion question)
    {
        if (!string.IsNullOrWhiteSpace(question.WidgetKey))
            return Allowed.FirstOrDefault(key => key.Equals(question.WidgetKey, StringComparison.OrdinalIgnoreCase))
                ?? question.WidgetKey;
        // Compatibility for saved questionnaires created before widgetKey existed.
        if (question.Type == DynamicQuestionType.Text)
        {
            if (question.Id is "bFitterAdditionalImage" or "bRemedialAdditionalImage") return CaptureDrawing;
            if (question.Id is "bFitterSigned" or "bFitterLintelSigned" or "bRemedialCustomerSigned") return CaptureSignature;
            if (question.Id.Equals("directive_complete", StringComparison.OrdinalIgnoreCase)) return MotorDeclaration;
        }
        if (question.Id.Equals("TimeArrived", StringComparison.OrdinalIgnoreCase) &&
            question.Type == DynamicQuestionType.Text) return TimePicker;
        if (question.Id.Equals("marks_out_of_10", StringComparison.OrdinalIgnoreCase) &&
            question.Type == DynamicQuestionType.ButtonPicker) return RatingTen;
        if (question.Type == DynamicQuestionType.Measurement &&
            question.Id is "uspot_appearence" or "uspot_qualityofworks" or "uspot_customersatisfaction")
            return RatingFive;
        if (question.Type == DynamicQuestionType.Information)
        {
            if (question.Id.Equals("photos_complete", StringComparison.OrdinalIgnoreCase)) return CapturePhotos;
            if (question.Id.Equals("signature_complete", StringComparison.OrdinalIgnoreCase)) return CaptureSignature;
            if (question.Id.StartsWith("rating_guide_", StringComparison.OrdinalIgnoreCase)) return RatingGuide;
        }
        return Default;
    }
}
