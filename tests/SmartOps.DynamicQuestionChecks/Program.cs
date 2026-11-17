using SmartOps.Shared.Models;

int assertions = 0;
void Check(bool condition, string message)
{
    assertions++;
    if (!condition) throw new Exception(message);
}

var definition = new DynamicQuestionDefinition
{
    Questions =
    [
        new DynamicQuestion { Id = "choice", Question = "Choose", Type = DynamicQuestionType.ButtonPicker,
            Required = true, Options = ["One", "Two"] },
        new DynamicQuestion { Id = "detail", Question = "Details", Type = DynamicQuestionType.Text,
            Required = true, ShowWhen = new() { QuestionId = "choice", Value = "Two" } },
        new DynamicQuestion { Id = "more", Question = "More", Type = DynamicQuestionType.Text,
            ShowWhenAll = [new() { QuestionId = "choice", Value = "Two" },
                           new() { QuestionId = "detail", Operator = DynamicConditionOperator.IsNotBlank }] },
        new DynamicQuestion { Id = "conditional", Question = "Conditional option",
            Type = DynamicQuestionType.Picker, Options = ["Fallback"], OptionRules =
            [
                new DynamicQuestionOptionRule
                {
                    Conditions = [new() { QuestionId = "choice", Value = "Two" }],
                    Options = ["Special"]
                }
            ] }
    ]
};
var answers = new List<DynamicAnswer>();
var session = new DynamicQuestionSession(definition, answers);
Check(!session.VisibleQuestions().Any(q => q.Id == "detail"), "Hidden before dependency selected");
Check(session.Validate().Count == 1, "Only visible required question is invalid");
session.SetAnswer("choice", "Two");
Check(session.VisibleQuestions().Any(q => q.Id == "detail"), "Conditional visibility follows answer");
Check(session.ResolveOptions(definition.Questions[3]).SequenceEqual(["Special"]), "Conditional options update");
Check(session.Validate().Any(x => x.QuestionId == "detail"), "Conditional required validation");
session.SetAnswer("detail", "Present");
Check(session.VisibleQuestions().Any(q => q.Id == "more"), "ShowWhenAll dependency evaluated");
Check(session.Validate().Count == 0, "Completed required fields validate");
session.SetAnswer("choice", "One");
Check(!session.VisibleQuestions().Any(q => q.Id == "detail"), "Visibility reverses");
Check(session.ResolveOptions(definition.Questions[3]).SequenceEqual(["Fallback"]), "Options revert");
Check(answers.Any(a => a.QuestionId == "detail" && a.Value == "Present"),
    "Hidden answer retained rather than destroyed");
Check(session.ReportEntries().All(x => x.Label != "Details"), "Hidden answer absent from report");
Check(!session.SetAnswer("unknown", "x"), "Unknown question may not mutate answers");
Check(!session.SetAnswer("choice", "One"), "Identical answer is not rewritten");
var original = DynamicQuestionCatalogue.CreateDefaults();
Check(DynamicQuestionCatalogue.Validate(original).Count == 0, "Default question catalogue remains valid");
Check(DynamicQuestionWidgets.For(new DynamicQuestion { Id = "TimeArrived", Type = DynamicQuestionType.Text })
    == DynamicQuestionWidgets.TimePicker, "Existing custom control remains compatible");
// Snapshot persistence is compatible with existing per-visit JSON columns.
string savedSnapshot = DynamicQuestionDocuments.WriteSnapshot(definition);
var restored = DynamicQuestionDocuments.ReadSnapshot(savedSnapshot);
Check(restored?.Questions.Count == definition.Questions.Count, "Snapshot round-trip");
Check(DynamicQuestionDocuments.ReadSnapshot("invalid json") is null, "Corrupt optional snapshot is ignored");
string savedAnswers = DynamicQuestionDocuments.WriteAnswers(answers);
Check(DynamicQuestionDocuments.ReadAnswers(savedAnswers).Count == answers.Count, "Answers round-trip");
Check(DynamicQuestionDocuments.ReadAnswers("invalid json").Count == 0, "Corrupt optional answers do not crash");
Check(DynamicQuestionWidgets.Supports(DynamicQuestionWidgets.CapturePhotos, DynamicQuestionType.Information),
    "Photograph widget is permitted for information questions");
Check(!DynamicQuestionWidgets.Supports(DynamicQuestionWidgets.CapturePhotos, DynamicQuestionType.Text),
    "Photograph widget cannot be assigned to text questions");
var invalidWidgetPackage = DynamicQuestionCatalogue.CreateDefaults();
invalidWidgetPackage.Sets["SpotCheck"].Questions[0].WidgetKey = "arbitrary-script";
Check(DynamicQuestionCatalogue.Validate(invalidWidgetPackage)
    .Any(e => e.Contains("unknown widget key")), "Unregistered actions are rejected");

var number = new DynamicQuestion
{
    Id = "count", Question = "Count", Type = DynamicQuestionType.PlusMinus,
    Required = false, Minimum = 0, Maximum = 4, DefaultValue = 0
};
var counterForm = new DynamicQuestionSession(new DynamicQuestionDefinition { Questions = [number] }, []);
Check(counterForm.Validate().Count == 1, "Uninitialised counter fails validation");
counterForm.SetAnswer("count", "2");
Check(counterForm.Validate().Count == 0, "Counter within range validates");
counterForm.SetAnswer("count", "8");
Check(counterForm.Validate().Count == 1, "Counter outside range fails validation");

Console.WriteLine($"Passed {assertions} DynamicQuestions checks.");
