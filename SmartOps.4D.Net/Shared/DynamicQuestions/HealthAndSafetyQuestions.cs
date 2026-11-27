using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class HealthAndSafetyQuestions
{
    public const string Key = "HealthAndSafety";

    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header) =>
        DynamicQuestionDocuments.ReadSnapshot(header.HealthAndSafetyQuestionsSnapshotJson);

    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header) =>
        DynamicQuestionDocuments.ReadAnswers(header.HealthAndSafetyAnswersJson);

    public static void Save(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        header.HealthAndSafetyAnswersJson = DynamicQuestionDocuments.WriteAnswers(answers);
        if (ReadSnapshot(header) is null)
        {
            header.HealthAndSafetyQuestionsSnapshotJson = DynamicQuestionDocuments.WriteSnapshot(definition);
            header.HealthAndSafetyQuestionSetVersion = DynamicQuestionLoader.CurrentVersion;
        }
    }

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) => JobQuestionnaireLogic.Validate(definition, answers);

    public static List<JobQuestionnaireLogic.ValidationIssue> ValidateIssues(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) =>
        JobQuestionnaireLogic.ValidateIssues(definition, answers);

    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header), ReadAnswers(header));
}
