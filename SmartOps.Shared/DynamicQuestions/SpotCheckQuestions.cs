using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class SpotCheckQuestions
{
    public const string Key = "SpotCheck";
    public static bool IsRating(DynamicQuestion question) => question.Type == DynamicQuestionType.Measurement
        && question.Id is "uspot_appearence" or "uspot_qualityofworks" or "uspot_customersatisfaction";

    public static DynamicQuestionDefinition Definition(string snapshot) => string.IsNullOrWhiteSpace(snapshot)
        ? DynamicQuestionLoader.Load(Key)
        : JsonSerializer.Deserialize<DynamicQuestionDefinition>(snapshot, DynamicQuestionLoader.JsonOptions)
            ?? throw new JsonException("Missing spot check questionnaire.");

    public static List<DynamicAnswer> Answers(string json) => string.IsNullOrWhiteSpace(json)
        ? [] : JsonSerializer.Deserialize<List<DynamicAnswer>>(json, DynamicQuestionLoader.JsonOptions) ?? [];

    public static string Value(IEnumerable<DynamicAnswer> answers, string id) =>
        answers.FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

    public static List<JobQuestionnaireLogic.ValidationIssue> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers,
        bool correctContract, bool signed, int photos)
    {
        var issues = JobQuestionnaireLogic.ValidateIssues(definition, answers);
        if (!correctContract) issues.Insert(0, new("contract", "Confirm the correct contract."));
        if (photos < 5) issues.Add(new("photos_complete", "5 photographs"));
        if (!signed) issues.Add(new("signature_complete", "Signature"));
        return issues;
    }
}
