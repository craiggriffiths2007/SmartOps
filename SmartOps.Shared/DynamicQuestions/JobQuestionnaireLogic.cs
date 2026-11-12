using System.Globalization;

namespace SmartOps.Shared.Models;

public static class JobQuestionnaireLogic
{
    public sealed record ValidationIssue(string QuestionId, string Message);

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers, IReadOnlyDictionary<string, string>? context = null) =>
        ValidateIssues(definition, answers, context).Select(issue => issue.Message).ToList();

    public static List<ValidationIssue> ValidateIssues(DynamicQuestionDefinition definition, List<DynamicAnswer> answers, IReadOnlyDictionary<string, string>? context = null)
    {
        var errors = new List<ValidationIssue>();
        foreach (var question in definition.Questions)
        {
            if (question.Type is DynamicQuestionType.Information or DynamicQuestionType.Image || !DynamicQuestionLogic.IsApplicable(question, answers, context)) continue;
            string value = answers.FirstOrDefault(a => a.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
            if (string.IsNullOrWhiteSpace(value)) { if (question.Required) errors.Add(new(question.Id, question.Question)); continue; }
            bool valid = question.Type switch {
                DynamicQuestionType.YesNo => DynamicWorkflowQuestions.IsBoolean(value),
                DynamicQuestionType.PlusMinus => PlusMinusQuestions.IsValid(question, value),
                DynamicQuestionType.Measurement => DynamicQuestionLogic.IsValidMeasurement(question, value),
                DynamicQuestionType.Picker => !question.ReadOnly || DynamicQuestionLogic.ResolveOptions(question, answers, context).Contains(value, StringComparer.OrdinalIgnoreCase),
                DynamicQuestionType.ButtonPicker => DynamicQuestionLogic.ResolveOptions(question, answers, context).Contains(value, StringComparer.OrdinalIgnoreCase),
                DynamicQuestionType.Date => DateTime.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                _ => true
            };
            if (!valid) errors.Add(new(question.Id, question.Question + " - check the answer"));
        }
        return errors;
    }

    public sealed record ReportEntry(string Label, string Value, bool IsInformation = false, string? Image = null);

    public static IReadOnlyList<ReportEntry> ReportEntries(DynamicQuestionDefinition? definition, List<DynamicAnswer> answers, IReadOnlyDictionary<string, string>? context = null)
    {
        if (definition is null) return [];
        var entries = new List<ReportEntry>();
        foreach (var question in definition.Questions.Where(q => DynamicQuestionLogic.IsApplicable(q, answers, context)))
        {
            if (question.Type == DynamicQuestionType.Information) { entries.Add(new(question.Question, "", true)); continue; }
            if (question.Type == DynamicQuestionType.Image)
            {
                foreach (string name in DynamicQuestionLogic.ResolveImages(question, answers, context))
                    if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9_-]+\.(png|jpg|jpeg|webp)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                        entries.Add(new(question.Question, "", Image: name));
                continue;
            }
            string value = answers.FirstOrDefault(a => a.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
            if (string.IsNullOrWhiteSpace(value)) { if (question.Required) entries.Add(new(question.Question, "Not recorded")); continue; }
            if (question.Type == DynamicQuestionType.YesNo) value = DynamicWorkflowQuestions.IsYes(value) ? "Yes" : "No";
            if (question.Type == DynamicQuestionType.Measurement && !string.IsNullOrWhiteSpace(question.Unit)) value += " " + question.Unit;
            entries.Add(new(question.Question, value));
        }
        return entries;
    }
}
