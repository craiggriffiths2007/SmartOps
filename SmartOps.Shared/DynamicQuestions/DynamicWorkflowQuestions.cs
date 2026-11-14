namespace SmartOps.Shared.Models;

public static class DynamicWorkflowQuestions
{
    public static bool IsWorkflow(DynamicQuestionType type) => type is
        DynamicQuestionType.ReplaceGlass or DynamicQuestionType.ReplacePanel or
        DynamicQuestionType.ReplaceLocking or DynamicQuestionType.Handles;
    public static bool IsYesNo(DynamicQuestionType type) => type == DynamicQuestionType.YesNo || IsWorkflow(type);
    public static bool IsYes(string? value) => value?.Trim().ToLowerInvariant() is "yes" or "true";
    public static bool IsBoolean(string? value) => value?.Trim().ToLowerInvariant() is "yes" or "no" or "true" or "false";

    public static bool SelectedType(SurveyItemBase item, DynamicQuestionType type)
    {
        var saved = DynamicQuestionSnapshots.Read(item);
        var definition = saved ?? DynamicQuestionLoader.Load(DynamicQuestionSnapshots.SetName(item) ?? "");
        var answers = item.GetSurveyAnswers();
        var context = DynamicQuestionSnapshots.Context(item);
        return definition.Questions.Any(q => q.Type == type && Selected(q, answers, context));
    }

    public static bool Selected(DynamicQuestion question, IEnumerable<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null) => IsWorkflow(question.Type)
        && DynamicQuestionLogic.IsApplicable(question, answers, context)
        && IsYes(answers.FirstOrDefault(a => a.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase))?.Value);
}
