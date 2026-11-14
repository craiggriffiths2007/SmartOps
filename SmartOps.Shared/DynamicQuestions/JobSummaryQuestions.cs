using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class JobSummaryQuestions
{
    public const string Key = "JobSummary";
    public const string SpecialRequirementsGateId = "HasSpecialRequirements";

    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.JobSummaryQuestionsSnapshotJson)) return null;
        try { return JsonSerializer.Deserialize<DynamicQuestionDefinition>(header.JobSummaryQuestionsSnapshotJson, DynamicQuestionLoader.JsonOptions); }
        catch (JsonException) { return null; }
    }

    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header)
    {
        if (string.IsNullOrWhiteSpace(header.JobSummaryAnswersJson)) return [];
        try { return JsonSerializer.Deserialize<List<DynamicAnswer>>(header.JobSummaryAnswersJson, DynamicQuestionLoader.JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    public static void Save(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        ApplyOperationalAnswers(header, definition, answers);
        header.JobSummaryAnswersJson = JsonSerializer.Serialize(answers, DynamicQuestionLoader.JsonOptions);
        if (ReadSnapshot(header) is null)
        {
            header.JobSummaryQuestionsSnapshotJson = JsonSerializer.Serialize(definition, DynamicQuestionLoader.JsonOptions);
            header.JobSummaryQuestionSetVersion = DynamicQuestionLoader.CurrentVersion;
        }
    }

    public static List<string> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers) =>
        ValidateIssues(definition, answers).Select(issue => issue.Message).ToList();

    public static List<JobQuestionnaireLogic.ValidationIssue> ValidateIssues(DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        var errors = JobQuestionnaireLogic.ValidateIssues(definition, answers);
        var time = definition.Questions.FirstOrDefault(q => q.Id == "TimeArrived");
        var value = answers.FirstOrDefault(a => a.QuestionId == "TimeArrived")?.Value;
        if (time is not null && DynamicQuestionLogic.IsApplicable(time, answers) && !string.IsNullOrWhiteSpace(value)
            && !TimeOnly.TryParseExact(value, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
            errors.Add(new(time.Id, time.Question + " must use HH:mm."));
        return errors;
    }

    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header), ReadAnswers(header));

    // These fields also drive attendance and completion outside this questionnaire.
    private static void ApplyOperationalAnswers(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        foreach (var question in definition.Questions.Where(q => DynamicQuestionLogic.IsApplicable(q, answers)))
        {
            var value = answers.FirstOrDefault(a => a.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
            switch (question.Id)
            {
                case "AgentCode": header.AgentCode = value; break;
                case "TimeArrived": header.TimeArrived = value; break;
                case "survey_complete": header.survey_complete = DynamicWorkflowQuestions.IsBoolean(value) ? DynamicWorkflowQuestions.IsYes(value) : null; break;
            }
        }
        // Removing the completion question leaves the ordinary full-survey workflow.
        if (!definition.Questions.Any(q => q.Id == "survey_complete")) header.survey_complete = true;
    }

    public static void SeedOperationalAnswers(JobHeaderBase header, DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        foreach (var (id, value) in new[] { ("AgentCode", header.AgentCode), ("TimeArrived", header.TimeArrived) })
        {
            if (!definition.Questions.Any(q => q.Id == id) || string.IsNullOrWhiteSpace(value)) continue;
            var answer = answers.FirstOrDefault(a => a.QuestionId == id);
            if (answer is null) answers.Add(new DynamicAnswer { QuestionId = id, Value = value });
            else answer.Value = value;
        }
    }
}

