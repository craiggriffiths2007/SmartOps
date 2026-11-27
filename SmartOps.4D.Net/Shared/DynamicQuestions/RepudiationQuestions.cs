using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class RepudiationQuestions
{
    public const string Key = "Repudiation";

    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header) =>
        DynamicQuestionDocuments.ReadSnapshot(header.RepudiationQuestionsSnapshotJson);

    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header) =>
        DynamicQuestionDocuments.ReadAnswers(header.RepudiationAnswersJson);

    public static void Save(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        header.RepudiationAnswersJson = DynamicQuestionDocuments.WriteAnswers(answers);
        if (ReadSnapshot(header) is null)
        {
            header.RepudiationQuestionsSnapshotJson = DynamicQuestionDocuments.WriteSnapshot(definition);
            header.RepudiationQuestionSetVersion = DynamicQuestionLoader.CurrentVersion;
        }
    }

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) => JobQuestionnaireLogic.Validate(definition, answers);

    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header), ReadAnswers(header));
}
