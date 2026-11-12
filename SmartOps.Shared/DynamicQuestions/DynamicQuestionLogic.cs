namespace SmartOps.Shared.Models;

public static class DynamicQuestionLogic
{
    public static bool IsApplicable(
        DynamicQuestion question,
        IEnumerable<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null)
    {
        return (question.ShowWhen == null || EvaluateCondition(question.ShowWhen, answers, context))
            && (question.ShowWhenAll ?? []).All(c => EvaluateCondition(c, answers, context));
    }

    // A $-prefixed condition references a page context value (e.g. $repair or
    // $opening_type), while ordinary IDs reference answers from the questionnaire.
    public static IReadOnlyList<string> ResolveOptions(
        DynamicQuestion question,
        IEnumerable<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null)
    {
        var answerList = answers as IReadOnlyList<DynamicAnswer> ?? answers.ToList();
        foreach (var rule in question.OptionRules ?? [])
        {
            if (rule.Conditions != null && rule.Conditions.All(c => EvaluateCondition(c, answerList, context)))
                return rule.Options ?? [];
        }
        return question.Options ?? [];
    }

    // An Image question never collects an answer. It resolves to bundled diagrams.
    public static IReadOnlyList<string> ResolveImages(
        DynamicQuestion question,
        IEnumerable<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null)
    {
        var answerList = answers as IReadOnlyList<DynamicAnswer> ?? answers.ToList();
        foreach (var rule in question.ImageRules ?? [])
            if (rule.Conditions != null &&
                rule.Conditions.All(c => EvaluateCondition(c, answerList, context)))
                return rule.Images ?? [];
        return string.IsNullOrWhiteSpace(question.Image) ? [] : [question.Image];
    }

    public static bool EvaluateCondition(
        DynamicQuestionCondition condition,
        IEnumerable<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null)
    {
        DynamicAnswer? answer =
            answers.FirstOrDefault(x =>
                x.QuestionId == condition.QuestionId);

        string value = condition.QuestionId.StartsWith('$') && context != null &&
            context.TryGetValue(condition.QuestionId, out var contextual)
            ? contextual?.Trim() ?? ""
            : answer?.Value?.Trim() ?? "";

        return condition.Operator switch
        {
            DynamicConditionOperator.Equals =>
                EqualsIgnoreCase(
                    value,
                    condition.Value),

            DynamicConditionOperator.NotEquals =>
                !EqualsIgnoreCase(
                    value,
                    condition.Value),

            DynamicConditionOperator.IsBlank =>
                string.IsNullOrWhiteSpace(value),

            DynamicConditionOperator.IsNotBlank =>
                !string.IsNullOrWhiteSpace(value),

            DynamicConditionOperator.NotBlankOrNone =>
                !string.IsNullOrWhiteSpace(value) &&
                !EqualsIgnoreCase(value, "None"),

            DynamicConditionOperator.Contains =>
                value.Contains(condition.Value ?? "", StringComparison.OrdinalIgnoreCase),

            // Pipe-delimited alternatives, for example "Door|French Doors".
            DynamicConditionOperator.In =>
                (condition.Value ?? "").Split('|', StringSplitOptions.TrimEntries)
                    .Any(candidate => EqualsIgnoreCase(value, candidate)),

            DynamicConditionOperator.GreaterThan =>
                double.TryParse(value, out double actual) &&
                double.TryParse(condition.Value, out double expected) &&
                actual > expected,

            _ => true
        };
    }

    public static bool IsValidMeasurement(DynamicQuestion question, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return !question.Required;
        if (!double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double number)
            && !double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.CurrentCulture, out number)
            || !double.IsFinite(number)) return false;
        return (!question.WholeNumber || number == Math.Truncate(number))
            && (!question.Minimum.HasValue || number >= question.Minimum.Value)
            && (!question.Maximum.HasValue || number <= question.Maximum.Value);
    }

    private static string NormalizeBoolean(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "yes" or "true" => "true",
        "no" or "false" => "false",
        _ => value?.Trim() ?? ""
    };

    private static bool EqualsIgnoreCase(
        string? left,
        string? right)
    {
        return string.Equals(
            NormalizeBoolean(left),
            NormalizeBoolean(right),
            StringComparison.OrdinalIgnoreCase);
    }
}