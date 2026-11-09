using SmartOps.Shared.Enums;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace SmartOps.Shared.Models;
public abstract class SurveyItemBase : TenantEntity
{
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string cause_of_damage { get => GetSurveyAnswerValue("cause_of_damage"); set => SetAnswer("cause_of_damage", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string room_location { get => GetSurveyAnswerValue("room_location"); set => SetAnswer("room_location", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual bool? collect_and_copy { get => AnswerBoolean("collect_and_copy"); set => SetAnswer("collect_and_copy", value.HasValue ? value.Value ? "Yes" : "No" : ""); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string temporary { get => GetSurveyAnswerValue("temporary"); set => SetAnswer("temporary", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string cause_of_damage_reason_different { get => GetSurveyAnswerValue("cause_of_damage_reason_different"); set => SetAnswer("cause_of_damage_reason_different", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual bool? bDifferentFromOriginal { get => AnswerBoolean("bDifferentFromOriginal"); set => SetAnswer("bDifferentFromOriginal", value.HasValue ? value.Value ? "Yes" : "No" : ""); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string ChangeItemTo { get => GetSurveyAnswerValue("ChangeItemTo"); set => SetAnswer("ChangeItemTo", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string print_name { get => GetSurveyAnswerValue("print_name"); set => SetAnswer("print_name", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual bool? point_of_entry { get => AnswerBoolean("point_of_entry"); set => SetAnswer("point_of_entry", value.HasValue ? value.Value ? "Yes" : "No" : ""); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string type_of_lockng_system_required { get => GetSurveyAnswerValue("type_of_lockng_system_required"); set => SetAnswer("type_of_lockng_system_required", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual bool? was_it_locked { get => AnswerBoolean("was_it_locked"); set => SetAnswer("was_it_locked", value.HasValue ? value.Value ? "Yes" : "No" : ""); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string Summary { get => GetSurveyAnswerValue("Summary"); set => SetAnswer("Summary", value); }
    [NotMapped, JsonIgnore, Browsable(false)] public virtual string parts_to_order { get => GetSurveyAnswerValue("parts_to_order"); set => SetAnswer("parts_to_order", value); }
    [Browsable(false)] public abstract enum_item_type item_type { get; }
    [Browsable(false)] public int HeaderId { get; set; }
    [Browsable(false)] public Guid Guid { get; set; } = Guid.NewGuid();
    [Browsable(false)] public string ContractCode { get; set; } = "";
    [Browsable(false)] public int item_number { get; set; }
    [Browsable(false)] public abstract string ItemTitle { get; }
    [Browsable(false)] public bool bComplete { get; set; }
    [Browsable(false)] public bool bDifferentFromOriginalSigned { get; set; }
    [Browsable(false)] public int no_of_photos { get; set; }
    [Browsable(false)] public int no_of_drawings { get; set; }
    private string _answersJson = "[]";
    private List<DynamicAnswer>? _answers;
    [Browsable(false)] public string SurveyAnswersJson {
        get => _answersJson;
        set { _answersJson = value; _answers = null; }
    }
    [Browsable(false)] public string? SurveyQuestionsSnapshotJson { get; set; }
    [Browsable(false)] public int? SurveyQuestionSetVersion { get; set; }
    private List<DynamicAnswer> Answers {
        get {
            if (_answers != null) return _answers;
            try { return _answers = JsonSerializer.Deserialize<List<DynamicAnswer>>(_answersJson) ?? []; }
            catch (JsonException) { return _answers = []; }
        }
    }
    public List<DynamicAnswer> GetSurveyAnswers() => Answers.Select(a => new DynamicAnswer {
        QuestionId = a.QuestionId, Value = a.Value, Source = a.Source, Confidence = a.Confidence }).ToList();
    public void SetSurveyAnswers(List<DynamicAnswer> answers) => SurveyAnswersJson = JsonSerializer.Serialize(answers);
    public string GetSurveyAnswerValue(string id) => Answers.FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
    protected bool? AnswerBoolean(string id) {
        string value = GetSurveyAnswerValue(id);
        return string.IsNullOrWhiteSpace(value) ? null : DynamicWorkflowQuestions.IsYes(value);
    }
    public void SetAnswer(string id, string value) {
        var answers = GetSurveyAnswers();
        var answer = answers.FirstOrDefault(a => a.QuestionId.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (answer == null) answers.Add(new() { QuestionId = id, Value = value });
        else answer.Value = value;
        SetSurveyAnswers(answers);
    }
}
