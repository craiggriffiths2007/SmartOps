using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class RepudiationQuestions
{
    public const string Key = "Repudiation";

    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.RepudiationQuestionsSnapshotJson)) return null;
        try { return JsonSerializer.Deserialize<DynamicQuestionDefinition>(header.RepudiationQuestionsSnapshotJson, DynamicQuestionLoader.JsonOptions); }
        catch (JsonException) { return null; }
    }

    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.RepudiationAnswersJson)) return [];
        try { return JsonSerializer.Deserialize<List<DynamicAnswer>>(header.RepudiationAnswersJson, DynamicQuestionLoader.JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    public static void Save(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        header.RepudiationAnswersJson = JsonSerializer.Serialize(answers, DynamicQuestionLoader.JsonOptions);
        if (ReadSnapshot(header) is null)
        {
            header.RepudiationQuestionsSnapshotJson = JsonSerializer.Serialize(definition, DynamicQuestionLoader.JsonOptions);
            header.RepudiationQuestionSetVersion = DynamicQuestionLoader.CurrentVersion;
        }
    }

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) => JobQuestionnaireLogic.Validate(definition, answers);

    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header), ReadAnswers(header));
}
