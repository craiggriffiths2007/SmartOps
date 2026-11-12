using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class HealthAndSafetyQuestions
{
    public const string Key = "HealthAndSafety";

    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.HealthAndSafetyQuestionsSnapshotJson)) return null;
        try { return JsonSerializer.Deserialize<DynamicQuestionDefinition>(header.HealthAndSafetyQuestionsSnapshotJson, DynamicQuestionLoader.JsonOptions); }
        catch (JsonException) { return null; }
    }

    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.HealthAndSafetyAnswersJson)) return [];
        try { return JsonSerializer.Deserialize<List<DynamicAnswer>>(header.HealthAndSafetyAnswersJson, DynamicQuestionLoader.JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    public static void Save(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        header.HealthAndSafetyAnswersJson = JsonSerializer.Serialize(answers, DynamicQuestionLoader.JsonOptions);
        if (ReadSnapshot(header) is null)
        {
            header.HealthAndSafetyQuestionsSnapshotJson = JsonSerializer.Serialize(definition, DynamicQuestionLoader.JsonOptions);
            header.HealthAndSafetyQuestionSetVersion = DynamicQuestionLoader.CurrentVersion;
        }
    }

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) => JobQuestionnaireLogic.Validate(definition, answers);

    public static List<JobQuestionnaireLogic.ValidationIssue> ValidateIssues(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) =>
        JobQuestionnaireLogic.ValidateIssues(definition, answers);

    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header), ReadAnswers(header));
}
