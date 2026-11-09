using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class GarageDoorMotorDeclarationQuestions
{
    public const string Key = "GarageDoorMotorDeclaration";

    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.GarageDoorMotorDeclarationQuestionsSnapshotJson)) return null;
        try { return JsonSerializer.Deserialize<DynamicQuestionDefinition>(header.GarageDoorMotorDeclarationQuestionsSnapshotJson, DynamicQuestionLoader.JsonOptions); }
        catch (JsonException) { return null; }
    }

    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.GarageDoorMotorDeclarationAnswersJson)) return [];
        try { return JsonSerializer.Deserialize<List<DynamicAnswer>>(header.GarageDoorMotorDeclarationAnswersJson, DynamicQuestionLoader.JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    public static void Save(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        header.GarageDoorMotorDeclarationAnswersJson = JsonSerializer.Serialize(answers, DynamicQuestionLoader.JsonOptions);
        if (ReadSnapshot(header) is null)
        {
            header.GarageDoorMotorDeclarationQuestionsSnapshotJson = JsonSerializer.Serialize(definition, DynamicQuestionLoader.JsonOptions);
            header.GarageDoorMotorDeclarationQuestionSetVersion = DynamicQuestionLoader.CurrentVersion;
        }
    }

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) => JobQuestionnaireLogic.Validate(definition, answers);

    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header), ReadAnswers(header));
}
