using System.ComponentModel;
using System.Text.Json;
using System.Globalization;
using SmartOps.Shared.Enums;

namespace SmartOps.Shared.Models;

/// <summary>
/// A visit owns its questionnaire. Never resolve an already-saved visit from a
/// newer tenant definition. Resurveys deliberately receive a *new* snapshot.
/// </summary>
public static class DynamicQuestionSnapshots
{
    public static DynamicQuestionDefinition? Read(SurveyItemBase item)
    {
        if (string.IsNullOrWhiteSpace(item.SurveyQuestionsSnapshotJson)) return null;
        try
        {
            var definition = JsonSerializer.Deserialize<DynamicQuestionDefinition>(
                item.SurveyQuestionsSnapshotJson, DynamicQuestionLoader.JsonOptions);
            return definition;
        }
        catch (JsonException) { return null; }
    }

    public static void Capture(SurveyItemBase item, DynamicQuestionDefinition definition, int version)
    {
        item.SurveyQuestionsSnapshotJson = JsonSerializer.Serialize(
            definition, DynamicQuestionLoader.JsonOptions);
        item.SurveyQuestionSetVersion = version;
    }

    public static string? SetName(SurveyItemBase item) => (item as CustomItemBase)?.QuestionSetKey;
    public static IReadOnlyDictionary<string, string> Context(SurveyItemBase item) => item is CustomItemBase custom
        ? Context(custom, custom.GetSurveyAnswers(), Read(custom) ?? DynamicQuestionLoader.Load(custom.QuestionSetKey))
        : new Dictionary<string, string>();

    // The editor already owns the answers and snapshot. Avoid parsing saved JSON
    // for every visibility/option check while the user is typing.
    public static IReadOnlyDictionary<string, string> Context(CustomItemBase custom,
        List<DynamicAnswer> answers, DynamicQuestionDefinition definition)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Answer(string id) => answers.FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
        string Boolean(string id, bool fallback = false) =>
            (string.IsNullOrWhiteSpace(Answer(id)) ? fallback : DynamicWorkflowQuestions.IsYes(Answer(id))).ToString();
        values["$repair"] = !custom.ParentItemGuid.HasValue
            && custom.QuestionSetKey.Equals(ItemAssemblyQuestions.Key, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(Answer("flex_repair")) ? "" : Boolean("flex_repair", custom.bRepair);
        values["$material"] = custom.QuestionSetKey.Equals(ItemAssemblyQuestions.Key, StringComparison.OrdinalIgnoreCase) ? Answer("material") : enum_material_type.None.ToString();
        values["$opening_type"] = Answer("opening_type");
        if (custom.QuestionSetKey.Equals("Glass", StringComparison.OrdinalIgnoreCase)) {
            values["$base_item"] = custom.ParentItemGuid.HasValue ? "custom" : "none";
            bool inherited = custom.ParentItemGuid.HasValue &&
                new[] { "UPVC", "Aluminium", "Timber", "Steel" }.Contains(custom.ParentMaterial, StringComparer.OrdinalIgnoreCase);
            values["$inherited_material"] = inherited.ToString();
            values["$material"] = inherited ? custom.ParentMaterial : Answer("ProductInto");
            values["$glass_stepped_allowed"] = (!custom.ParentItemGuid.HasValue || values["$material"].Equals("Timber", StringComparison.OrdinalIgnoreCase)).ToString();
        }
        values["$fensa"] = Boolean("flex_fensa", true);
        values["$replace_glass"] = (definition.Questions.Any(q => q.Type == DynamicQuestionType.ReplaceGlass
            && DynamicWorkflowQuestions.IsYes(Answer(q.Id)))).ToString();
        values["$bifold_doors"] = Answer("number_of_doors");
        values["$conservatory_roof"] = Answer("type");
        values["$conservatory_wall"] = Answer("wall_pos");
        values["$garage_opening"] = Answer("opening_type");
        values["$sub_item"] = custom.ParentItemGuid.HasValue.ToString();
        values["$closing_questions"] = (!custom.ParentItemGuid.HasValue).ToString();
        string cause = Answer("cause_of_damage");
        values["$damage_different"] = (!string.IsNullOrWhiteSpace(cause) && cause != "Domestic"
            && !string.IsNullOrWhiteSpace(custom.ContractCauseOfDamage) && custom.ContractCauseOfDamage != "Unknown"
            && !cause.Equals(custom.ContractCauseOfDamage, StringComparison.OrdinalIgnoreCase)).ToString();
        values["$security_required"] = (cause == "Theft" && custom.RequiresSecuritySurvey).ToString();
        return values;
    }

    /// <summary>
    /// Called ONLY on a detached copy for a new survey visit. Clears removed,
    /// incompatible, hidden and no-longer-available answers. Never modifies
    /// the original completed survey in the database.
    /// </summary>
    public static void UpgradeResurvey(SurveyItemBase item, DynamicQuestionDefinition latest, int version)
    {
        var previous = Read(item);
        var oldQuestions = previous?.Questions.ToDictionary(x => x.Id,
            StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, DynamicQuestion>(StringComparer.OrdinalIgnoreCase);
        var valid = latest.Questions.Where(q => q.Type is not (DynamicQuestionType.Image or DynamicQuestionType.Information))
            .ToDictionary(q => q.Id, StringComparer.OrdinalIgnoreCase);
        var answers = GetAnswers(item).Where(a => valid.TryGetValue(a.QuestionId, out var newQuestion)
                && (!oldQuestions.TryGetValue(a.QuestionId, out var oldQuestion)
                    || Compatible(oldQuestion.Type, newQuestion.Type)))
            .GroupBy(a => a.QuestionId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()).ToList();

        // Resolve dependent options/visibility with the newly retained answers.
        // Iterate because dropping a parent can invalidate another question.
        var context = Context(item);
        for (int attempt = 0; attempt <= valid.Count; attempt++)
        {
            int before = answers.Count;
            answers = answers.Where(a =>
            {
                var q = valid[a.QuestionId];
                if (!DynamicQuestionLogic.IsApplicable(q, answers, context)) return false;
                if (DynamicWorkflowQuestions.IsYesNo(q.Type))
                    return new[] { "true", "false", "yes", "no" }.Contains(
                        a.Value.Trim().ToLowerInvariant());
                if (q.Type == DynamicQuestionType.PlusMinus) return PlusMinusQuestions.IsValid(q, a.Value);
                if (q.Type == DynamicQuestionType.Measurement)
                    return DynamicQuestionLogic.IsValidMeasurement(q, a.Value);
                if (q.Type == DynamicQuestionType.ButtonPicker || q.Type == DynamicQuestionType.Picker && q.ReadOnly)
                    return DynamicQuestionLogic.ResolveOptions(q, answers, context)
                        .Contains(a.Value, StringComparer.OrdinalIgnoreCase);
                return true;
            }).ToList();
            if (before == answers.Count) break;
        }
        SetAnswers(item, answers);
        Capture(item, latest, version);
    }

    private static bool Compatible(DynamicQuestionType oldType, DynamicQuestionType newType) =>
        oldType == newType ||
        (oldType is DynamicQuestionType.Measurement or DynamicQuestionType.PlusMinus) &&
        (newType is DynamicQuestionType.Measurement or DynamicQuestionType.PlusMinus) ||
        (DynamicWorkflowQuestions.IsYesNo(oldType) && DynamicWorkflowQuestions.IsYesNo(newType)) ||
        (oldType is DynamicQuestionType.Picker or DynamicQuestionType.ButtonPicker) &&
        (newType is DynamicQuestionType.Picker or DynamicQuestionType.ButtonPicker);

    public static List<DynamicAnswer> GetAnswers(SurveyItemBase item) => item.GetSurveyAnswers();

    private static void SetAnswers(SurveyItemBase item, List<DynamicAnswer> answers) => item.SetSurveyAnswers(answers);
}
