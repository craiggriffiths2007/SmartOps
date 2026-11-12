using System.Text.Json;

namespace SmartOps.Shared.Models;

/// <summary>Dynamic answers and visit snapshots, with explicit links to active fitting workflows.</summary>
public static class FittingQuestionnaires
{
    public static readonly string[] Keys = ["FitterCompletion", "FitterStockUsage", "RemedialCompletion"];

    private static string SnapshotJson(JobHeaderBase header, string key) => key switch {
        "FitterCompletion" => header.FitterCompletionQuestionsSnapshotJson,
        "FitterStockUsage" => header.FitterStockUsageQuestionsSnapshotJson,
        "RemedialCompletion" => header.RemedialCompletionQuestionsSnapshotJson,
        _ => throw new ArgumentException("Unknown fitting questionnaire.", nameof(key))
    };
    private static string AnswersJson(JobHeaderBase header, string key) => key switch {
        "FitterCompletion" => header.FitterCompletionAnswersJson,
        "FitterStockUsage" => header.FitterStockUsageAnswersJson,
        "RemedialCompletion" => header.RemedialCompletionAnswersJson,
        _ => throw new ArgumentException("Unknown fitting questionnaire.", nameof(key))
    };
    public static DynamicQuestionDefinition? ReadSnapshot(JobHeaderBase header, string key)
    {
        string json = SnapshotJson(header, key);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<DynamicQuestionDefinition>(json, DynamicQuestionLoader.JsonOptions); }
        catch (JsonException) { return null; }
    }
    public static List<DynamicAnswer> ReadAnswers(JobHeaderBase header, string key)
    {
        string json = AnswersJson(header, key);
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<DynamicAnswer>>(json, DynamicQuestionLoader.JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }
    // Attendance is stored on the header because the customer signature uses it.
    public static void DefaultAttendanceTimes(JobHeaderBase header)
    {
        static string ScheduledTime(string value) =>
            TimeSpan.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var time)
            && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1)
                ? time.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture) : "";

        if (string.IsNullOrWhiteSpace(header.FitTimeArrived))
            header.FitTimeArrived = ScheduledTime(header.StartTime);
        if (string.IsNullOrWhiteSpace(header.FitTimeLeft))
            header.FitTimeLeft = ScheduledTime(header.FinishTime);
    }
    public static void RefreshOperationalAnswers(JobHeaderBase header, string key, List<DynamicAnswer> answers)
    {
        // These values are recorded by native drawing/signature pages, never by typed answers.
        (string Id, bool Recorded)[] actions = key switch {
            "FitterCompletion" => [("bFitterAdditionalImage", header.bFitterAdditionalImage), ("bFitterSigned", header.bFitterSigned), ("directive_complete", header.directive_complete == 1)],
            "FitterStockUsage" => [("bFitterLintelSigned", header.bFitterLintelSigned)],
            "RemedialCompletion" => [("bRemedialAdditionalImage", header.bRemedialAdditionalImage), ("bRemedialCustomerSigned", header.bRemedialCustomerSigned)],
            _ => throw new ArgumentException("Unknown fitting questionnaire.", nameof(key))
        };
        foreach (var (id, recorded) in actions) {
            string value = recorded ? "Recorded" : "";
            var answer = answers.FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (answer is null) answers.Add(new() { QuestionId = id, Value = value });
            else answer.Value = value;
        }
    }
    public static void Save(JobHeaderBase header, string key, DynamicQuestionDefinition definition, List<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null)
    {
        // Job completion and motor availability also drive navigation outside the form.
        foreach (var question in definition.Questions.Where(q => DynamicQuestionLogic.IsApplicable(q, answers, context))) {
            string? value = answers.FirstOrDefault(a => a.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase))?.Value;
            if (!DynamicWorkflowQuestions.IsBoolean(value)) continue;
            if (key == "FitterCompletion" && question.Id == "bFitterJobComplete") header.bFitterJobComplete = DynamicWorkflowQuestions.IsYes(value);
            if (key == "FitterCompletion" && question.Id == "garage_door_motor") header.garage_door_motor = DynamicWorkflowQuestions.IsYes(value);
        }
        RefreshOperationalAnswers(header, key, answers);
        var ids = definition.Questions.Where(q => q.Type is not (DynamicQuestionType.Information or DynamicQuestionType.Image))
            .Select(q => q.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string json = JsonSerializer.Serialize(answers.Where(a => ids.Contains(a.QuestionId)).ToList(), DynamicQuestionLoader.JsonOptions);
        bool capture = ReadSnapshot(header, key) is null;
        string snapshot = capture ? JsonSerializer.Serialize(definition, DynamicQuestionLoader.JsonOptions) : "";
        switch (key) {
            case "FitterCompletion":
                header.FitterCompletionAnswersJson = json;
                if (capture) { header.FitterCompletionQuestionsSnapshotJson = snapshot; header.FitterCompletionQuestionSetVersion = DynamicQuestionLoader.CurrentVersion; }
                break;
            case "FitterStockUsage":
                header.FitterStockUsageAnswersJson = json;
                if (capture) { header.FitterStockUsageQuestionsSnapshotJson = snapshot; header.FitterStockUsageQuestionSetVersion = DynamicQuestionLoader.CurrentVersion; }
                break;
            case "RemedialCompletion":
                header.RemedialCompletionAnswersJson = json;
                if (capture) { header.RemedialCompletionQuestionsSnapshotJson = snapshot; header.RemedialCompletionQuestionSetVersion = DynamicQuestionLoader.CurrentVersion; }
                break;
        }
    }
    public static List<string> Validate(JobHeaderBase header, string key, DynamicQuestionDefinition definition, List<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null) =>
        ValidateIssues(header, key, definition, answers, context).Select(issue => issue.Message).ToList();

    public static List<JobQuestionnaireLogic.ValidationIssue> ValidateIssues(JobHeaderBase header, string key, DynamicQuestionDefinition definition, List<DynamicAnswer> answers,
        IReadOnlyDictionary<string, string>? context = null)
    {
        RefreshOperationalAnswers(header, key, answers);
        return JobQuestionnaireLogic.ValidateIssues(definition, answers, context);
    }
    public static void SetComplete(JobHeaderBase header, string key, bool complete)
    {
        switch (key) {
            case "FitterCompletion": header.bFitterCompletionFormComplete = complete; break;
            case "FitterStockUsage": header.bFitterStockUsageComplete = complete; break;
            case "RemedialCompletion": header.bRemedialCompletionFormComplete = complete; break;
        }
    }
    public static void Reset(JobHeaderBase header)
    {
        header.FitterCompletionAnswersJson = header.FitterCompletionQuestionsSnapshotJson = "";
        header.FitterStockUsageAnswersJson = header.FitterStockUsageQuestionsSnapshotJson = "";
        header.RemedialCompletionAnswersJson = header.RemedialCompletionQuestionsSnapshotJson = "";
        header.FitterCompletionQuestionSetVersion = header.FitterStockUsageQuestionSetVersion = header.RemedialCompletionQuestionSetVersion = null;
        foreach (string key in Keys) SetComplete(header, key, false);
    }
    public static IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(JobHeaderBase header, string key) =>
        JobQuestionnaireLogic.ReportEntries(ReadSnapshot(header, key), ReadAnswers(header, key), new Dictionary<string, string> {
            ["$mandate_signed"] = (header.bFitterMandateSigned == true).ToString(), ["$has_garage_items"] = "True"
        });
}
