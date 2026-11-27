using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class WorkAccidentQuestions
{
    public const string AccidentKey = "WorkAccident";
    public const string NearMissKey = "NearMiss";
    public static bool IsNearMiss(string type) => type.Equals("Nearmiss", StringComparison.OrdinalIgnoreCase);
    public static string Key(string type) => IsNearMiss(type) ? NearMissKey : AccidentKey;

    public static DynamicQuestionDefinition Definition(string snapshot, string type) => string.IsNullOrWhiteSpace(snapshot)
        ? DynamicQuestionLoader.Load(Key(type))
        : JsonSerializer.Deserialize<DynamicQuestionDefinition>(snapshot, DynamicQuestionLoader.JsonOptions)
            ?? throw new JsonException("Missing work accident questionnaire.");

    public static List<DynamicAnswer> Answers(string json) => string.IsNullOrWhiteSpace(json)
        ? [] : JsonSerializer.Deserialize<List<DynamicAnswer>>(json, DynamicQuestionLoader.JsonOptions) ?? [];

    public static string Value(IEnumerable<DynamicAnswer> answers, string id) =>
        answers.FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

    public static List<JobQuestionnaireLogic.ValidationIssue> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        var issues = JobQuestionnaireLogic.ValidateIssues(definition, answers);
        var timeQuestion = definition.Questions.FirstOrDefault(q => q.Id == "time_happened");
        if (timeQuestion != null && DynamicQuestionLogic.IsApplicable(timeQuestion, answers))
        {
            string value = Value(answers, "time_happened");
            if ((timeQuestion.Required || !string.IsNullOrWhiteSpace(value)) &&
                (!TimeSpan.TryParse(value, out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)))
                issues.Add(new("time_happened", "Enter a valid time for the accident."));
        }
        return issues.DistinctBy(i => i.QuestionId).ToList();
    }
}
