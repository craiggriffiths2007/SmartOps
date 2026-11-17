using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class GarageDoorMotorDeclarationQuestions
{
    public const string Key = "GarageDoorMotorDeclaration";

    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header) =>
        DynamicQuestionDocuments.ReadSnapshot(header.GarageDoorMotorDeclarationQuestionsSnapshotJson);

    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header) =>
        DynamicQuestionDocuments.ReadAnswers(header.GarageDoorMotorDeclarationAnswersJson);

    public static void Save(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        header.GarageDoorMotorDeclarationAnswersJson = DynamicQuestionDocuments.WriteAnswers(answers);
        if (ReadSnapshot(header) is null)
        {
            header.GarageDoorMotorDeclarationQuestionsSnapshotJson = DynamicQuestionDocuments.WriteSnapshot(definition);
            header.GarageDoorMotorDeclarationQuestionSetVersion = DynamicQuestionLoader.CurrentVersion;
        }
    }

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) => JobQuestionnaireLogic.Validate(definition, answers);

    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header), ReadAnswers(header));
}
