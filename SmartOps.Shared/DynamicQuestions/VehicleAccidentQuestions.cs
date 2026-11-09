using System.Text.Json;

namespace SmartOps.Shared.Models;

public static class VehicleAccidentQuestions
{
    public const string Key = "VehicleAccident";
    public const int MaximumWitnesses = 3;

    public static DynamicQuestionDefinition Definition(string snapshot)
    {
        var definition = string.IsNullOrWhiteSpace(snapshot)
            ? DynamicQuestionLoader.Load(Key)
            : JsonSerializer.Deserialize<DynamicQuestionDefinition>(snapshot, DynamicQuestionLoader.JsonOptions)
                ?? throw new JsonException("Missing vehicle accident questionnaire.");
        // The witness limit is a product constraint, including for tenant-customised forms.
        var count = definition.Questions.FirstOrDefault(q => q.Id == "witness_count");
        if (count is null) throw new InvalidOperationException("Vehicle accident questions require witness_count.");
        count.Type = DynamicQuestionType.PlusMinus;
        count.Required = true;
        count.Minimum = 0;
        count.Maximum = MaximumWitnesses;
        count.DefaultValue = 0;
        count.WholeNumber = true;
        count.Options = [];
        count.OptionRules = [];
        count.ShowWhen = null;
        count.ShowWhenAll = [];
        return definition;
    }

    public static List<DynamicAnswer> Answers(string json) => string.IsNullOrWhiteSpace(json)
        ? [] : JsonSerializer.Deserialize<List<DynamicAnswer>>(json, DynamicQuestionLoader.JsonOptions) ?? [];

    public static string Value(IEnumerable<DynamicAnswer> answers, string id) =>
        answers.FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";

    public static List<JobQuestionnaireLogic.ValidationIssue> Validate(DynamicQuestionDefinition definition, List<DynamicAnswer> answers)
    {
        var issues = JobQuestionnaireLogic.ValidateIssues(definition, answers);
        if (!int.TryParse(Value(answers, "witness_count"), out int count) || count < 0 || count > MaximumWitnesses)
            issues.Add(new("witness_count", "Select between zero and three witnesses."));
        if (!DynamicWorkflowQuestions.IsYes(Value(answers, "declaration_confirmed")))
            issues.Add(new("declaration_confirmed", "Confirm the driver declaration."));
        if (!TimeSpan.TryParse(Value(answers, "acc_time"), out var time) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1))
            issues.Add(new("acc_time", "Enter a valid accident time."));
        return issues.DistinctBy(i => i.QuestionId).ToList();
    }
}
