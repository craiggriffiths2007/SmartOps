namespace SmartOps.Shared.Models;

/// <summary>
/// Platform-independent questionnaire state. MAUI, WPF and web reports share the
/// same visibility, conditional options, answer mutation and validation rules.
/// Does not own persistence or change the caller's snapshot/version.
/// </summary>
public sealed class DynamicQuestionSession
{
    private readonly Dictionary<string, DynamicQuestion> _questions;
    public DynamicQuestionDefinition Definition { get; }
    public List<DynamicAnswer> Answers { get; }
    public IReadOnlyDictionary<string, string>? Context { get; set; }

    public event EventHandler<DynamicQuestionAnswerChangedEventArgs>? AnswerChanged;

    public DynamicQuestionSession(DynamicQuestionDefinition definition,
        List<DynamicAnswer> answers, IReadOnlyDictionary<string, string>? context = null)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Answers = answers ?? throw new ArgumentNullException(nameof(answers));
        Context = context;
        // Historical visit snapshots can contain old/duplicate IDs; presenting
        // them must not crash the entire report. Publishing still validates IDs.
        _questions = definition.Questions
            .Where(q => !string.IsNullOrWhiteSpace(q.Id))
            .GroupBy(q => q.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public DynamicAnswer EnsureAnswer(DynamicQuestion question)
    {
        var answer = Answers.FirstOrDefault(a =>
            a.QuestionId.Equals(question.Id, StringComparison.OrdinalIgnoreCase));
        if (answer is not null) return answer;
        answer = new DynamicAnswer { QuestionId = question.Id };
        Answers.Add(answer);
        return answer;
    }

    public string Value(string id) => Answers.FirstOrDefault(a =>
        a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

    public bool SetAnswer(string id, string? value,
        DynamicAnswerSource source = DynamicAnswerSource.Surveyor)
    {
        if (!_questions.TryGetValue(id, out var question) ||
            question.Type is DynamicQuestionType.Information or DynamicQuestionType.Image)
            return false;

        var answer = EnsureAnswer(question);
        value ??= "";
        if (string.Equals(answer.Value, value, StringComparison.Ordinal) && answer.Source == source)
            return false;
        answer.Value = value;
        answer.Source = source;
        AnswerChanged?.Invoke(this, new DynamicQuestionAnswerChangedEventArgs(question.Id));
        return true;
    }

    public bool IsApplicable(DynamicQuestion question) =>
        DynamicQuestionLogic.IsApplicable(question, Answers, Context);

    public IReadOnlyList<string> ResolveOptions(DynamicQuestion question) =>
        DynamicQuestionLogic.ResolveOptions(question, Answers, Context);

    public IReadOnlyList<string> ResolveImages(DynamicQuestion question) =>
        DynamicQuestionLogic.ResolveImages(question, Answers, Context);

    public IEnumerable<DynamicQuestion> VisibleQuestions() =>
        Definition.Questions.Where(IsApplicable);

    public List<JobQuestionnaireLogic.ValidationIssue> Validate() =>
        JobQuestionnaireLogic.ValidateIssues(Definition, Answers, Context);

    public IReadOnlyList<JobQuestionnaireLogic.ReportEntry> ReportEntries(
        bool includeMissingRequired = true, bool includeMeasurementUnits = true) =>
        JobQuestionnaireLogic.ReportEntries(Definition, Answers, Context,
            includeMissingRequired, includeMeasurementUnits);
}

public sealed class DynamicQuestionAnswerChangedEventArgs(string questionId) : EventArgs
{
    public string QuestionId { get; } = questionId;
}
