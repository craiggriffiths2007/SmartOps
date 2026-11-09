namespace SmartOps.Shared.Models
{
    public class DynamicQuestionDefinition
    {
        public List<DynamicQuestion> Questions { get; set; } = [];
        public bool IncludeHeaderQuestions { get; set; } = true;
        public bool IncludeFooterQuestions { get; set; } = true;
    }
    public class DynamicQuestion
    {
        public string Id { get; set; } = "";
        public string Question { get; set; } = "";
        public DynamicQuestionType Type { get; set; }
        public bool Required { get; set; }
        // Prevent typing while retaining dropdown selection.
        public bool ReadOnly { get; set; }
        public List<string> Options { get; set; } = [];

        // First matching rule replaces Options; conditions within a rule are ANDed.
        public List<DynamicQuestionOptionRule> OptionRules { get; set; } = [];

        // Display-only diagrams. These are bundled MAUI image asset filenames.
        public string Image { get; set; } = "";
        // The first matching rule supplies a list of diagrams.
        public List<DynamicQuestionImageRule> ImageRules { get; set; } = [];

        public DynamicQuestionCondition? ShowWhen { get; set; }
        // Additional visibility conditions must all match, together with ShowWhen.
        public List<DynamicQuestionCondition> ShowWhenAll { get; set; } = [];
        public string Unit { get; set; } = "mm";
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public bool WholeNumber { get; set; }
        public int? DefaultValue { get; set; }
    }

    public class DynamicQuestionOptionRule
    {
        public List<DynamicQuestionCondition> Conditions { get; set; } = [];
        public List<string> Options { get; set; } = [];
    }

    public class DynamicQuestionImageRule
    {
        public List<DynamicQuestionCondition> Conditions { get; set; } = [];
        public List<string> Images { get; set; } = [];
    }

    public enum DynamicConditionOperator
    {
        Equals,
        NotEquals,
        IsBlank,
        IsNotBlank,
        NotBlankOrNone,
        GreaterThan,
        Contains,
        In
    }

    public class DynamicQuestionCondition
    {
        public string QuestionId { get; set; } = "";

        public DynamicConditionOperator Operator { get; set; }
            = DynamicConditionOperator.Equals;

        public string Value { get; set; } = "";
    }

    public class DynamicAnswer
    {
        public string QuestionId { get; set; } = "";
        public string Value { get; set; } = "";
        public DynamicAnswerSource Source { get; set; }
        public double? Confidence { get; set; }
    }

    public enum DynamicQuestionType
    {
        Text,
        YesNo,
        Picker,
        Measurement,
        ButtonPicker,
        Date,
        Image,
        Information,
        ReplaceGlass,
        ReplacePanel,
        ReplaceLocking,
        Handles,
        PlusMinus
    }

    public enum DynamicAnswerSource
    {
        Surveyor,
        AI,
        Imported
    }
}
