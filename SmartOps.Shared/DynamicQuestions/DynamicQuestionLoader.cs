using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartOps.Shared.Models;

/// <summary>
/// Embedded JSON remains the fallback. The survey app may install one complete
/// tenant-specific snapshot; the web server must NOT install a global snapshot.
/// </summary>
public static class DynamicQuestionLoader
{
    private static readonly object Sync = new();
    private static DynamicQuestionSetPackage? _active;

    public static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static void SetCurrent(DynamicQuestionSetPackage? package)
    {
        if (package != null)
        {
            DynamicQuestionCatalogue.EnsureNewCategories(package);
        }
        lock (Sync)
        {
            _active = package;
        }
    }

    public static IReadOnlyList<string> AvailableItemSets
    {
        get { lock (Sync) return (_active?.Sets.Keys.AsEnumerable() ?? DynamicQuestionCatalogue.Keys)
            .Where(DynamicQuestionCatalogue.IsItemSet).ToArray(); }
    }

    public static int CurrentVersion
    {
        get { lock (Sync) return _active?.Version ?? 1; }
    }

    public static DynamicQuestionDefinition Load(string itemType)
    {
        lock (Sync)
        {
            if (_active?.Sets.TryGetValue(itemType, out var custom) == true)
                return SharedItemQuestions.Compose(itemType, custom, _active);
        }

        return DynamicQuestionCatalogue.Keys.Contains(itemType, StringComparer.OrdinalIgnoreCase)
            ? LoadDefault(itemType) : new DynamicQuestionDefinition();
    }

    public static DynamicQuestionDefinition LoadDefault(string itemType)
    {
        var definition = LoadDefaultGroup(itemType);
        return SharedItemQuestions.Compose(itemType, definition);
    }

    // The editor and downloaded package store shared groups separately.
    public static DynamicQuestionDefinition LoadDefaultGroup(string itemType)
    {
        Assembly assembly = typeof(DynamicQuestionLoader).Assembly;
        string fileName = $".{itemType}Questions.json";
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.Contains(".DynamicQuestions.", StringComparison.Ordinal)
                && name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException($"Question resource '{itemType}' was not found.");

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Unable to open '{resourceName}'.");
        var definition = JsonSerializer.Deserialize<DynamicQuestionDefinition>(stream, JsonOptions)
            ?? new DynamicQuestionDefinition();
        return definition;
    }

    public static DynamicQuestionDefinition Copy(DynamicQuestionDefinition definition) =>
        JsonSerializer.Deserialize<DynamicQuestionDefinition>(
            JsonSerializer.Serialize(definition, JsonOptions), JsonOptions) ?? new();
}

public sealed class DynamicQuestionSetPackage
{
    public int Version { get; set; } = 1;
    public Dictionary<string, DynamicQuestionDefinition> Sets { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public static class DynamicQuestionCatalogue
{
    public static IReadOnlyList<string> Keys { get; } = typeof(DynamicQuestionLoader)
        .Assembly.GetManifestResourceNames()
        .Where(x => x.Contains(".DynamicQuestions.", StringComparison.Ordinal)
            && x.EndsWith("Questions.json", StringComparison.OrdinalIgnoreCase))
        .Select(x => x.Split('.')[^2].Replace("Questions", "", StringComparison.OrdinalIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(x => x)
        .ToArray();

    public static DynamicQuestionSetPackage CreateDefaults()
    {
        var package = new DynamicQuestionSetPackage();
        foreach (var key in Keys)
            package.Sets[key] = DynamicQuestionLoader.LoadDefaultGroup(key);
        return package;
    }

    /// <summary>Add missing categories without changing custom questions or deleted choices.</summary>
    public static bool EnsureNewCategories(DynamicQuestionSetPackage package)
    {
        bool changed = package.Sets.Remove("SpecialRequirements");
        if (package.Sets.TryGetValue("Footer", out var footer))
            changed |= footer.Questions.RemoveAll(q => q.Id == "print_name") > 0;
        if (package.Sets.TryGetValue(JobSummaryQuestions.Key, out var summary) &&
            !summary.Questions.Any(q => q.Id == JobSummaryQuestions.SpecialRequirementsGateId))
        {
            var section = DynamicQuestionLoader.LoadDefaultGroup(JobSummaryQuestions.Key).Questions
                .Where(q => q.Id == JobSummaryQuestions.SpecialRequirementsGateId || q.Id.StartsWith("sr_", StringComparison.Ordinal));
            int index = summary.Questions.FindIndex(q => q.Id == "Summary");
            summary.Questions.InsertRange(index < 0 ? summary.Questions.Count : index + 1, section);
            changed = true;
        }
        foreach (string key in Keys.Where(key => !IsItemSet(key)))
        {
            if (package.Sets.ContainsKey(key)) continue;
            package.Sets[key] = DynamicQuestionLoader.LoadDefaultGroup(key);
            changed = true;
        }
        return changed;
    }

    public static IReadOnlySet<string> FlexibleBuiltInKeys { get; } = new HashSet<string>(
        ["Garage", "Bifolding", "Composite", "Conservatory", "ItemAssembly", "Glass", "Panel", "Lockmech"], StringComparer.OrdinalIgnoreCase);

    public static bool IsFlexibleItemSet(string key) => FlexibleBuiltInKeys.Contains(key) ||
        (IsItemSet(key) && !Keys.Contains(key, StringComparer.OrdinalIgnoreCase));

    public static bool SupportsWorkflow(string key, DynamicQuestionType type) => IsItemSet(key) &&
        (type == DynamicQuestionType.Handles || IsFlexibleItemSet(key) && !new[] { "Glass", "Panel", "Lockmech" }.Contains(key, StringComparer.OrdinalIgnoreCase));

    public static bool IsItemSet(string key) => !NonItemKeys.Contains(key);

    public static IReadOnlySet<string> NonItemKeys { get; } = new HashSet<string>(
        ["SpotCheck", "WorkAccident", "NearMiss", "VehicleAccident", "ToolCheck", "SalesCar", "FitterVan", "DeliveryVan", "DeliveryHGV", "Header", "Footer", "HealthAndSafety", "JobSummary", "Repudiation", "GarageDoorMotorDeclaration", "FitterCompletion", "FitterStockUsage", "RemedialCompletion"], StringComparer.OrdinalIgnoreCase);

    public static bool IsValidName(string key) => !string.IsNullOrWhiteSpace(key) &&
        key.Length <= 80 && key == key.Trim() &&
        System.Text.RegularExpressions.Regex.IsMatch(key, @"^[A-Za-z][A-Za-z0-9 _-]*$") &&
        key is not ("__proto__" or "constructor" or "prototype");

    public static readonly IReadOnlyDictionary<string, string> ItemLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["ItemAssembly"] = "Assembly", ["Glass"] = "Glass", ["Panel"] = "Panel",
            ["Lockmech"] = "Locking System", ["Garage"] = "Garage Door",
            ["Bifolding"] = "Bifolding Door", ["Composite"] = "Composite Door",
            ["Conservatory"] = "Conservatory Roof"
        };

    public static string ItemLabel(string key) => ItemLabels.TryGetValue(key, out var label) ? label : key;

    public static readonly IReadOnlySet<string> AllowedContexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "$has_garage_items", "$mandate_signed", "$inherited_material", "$material", "$opening_type", "$repair", "$fensa", "$base_item",
        "$bifold_doors", "$conservatory_roof", "$conservatory_wall", "$garage_opening", "$replace_glass", "$glass_stepped_allowed", "$damage_different", "$security_required", "$sub_item", "$closing_questions"
    };

    public static List<string> Validate(DynamicQuestionSetPackage package)
    {
        var errors = new List<string>();
        if (package.Sets is null) return ["Missing question sets."];
        if (package.Sets.Count > 100) errors.Add("Maximum 100 question groups.");
        foreach (var required in Keys.Where(k => !IsItemSet(k)))
            if (!package.Sets.ContainsKey(required)) errors.Add($"Missing question set: {required}.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, definition) in package.Sets)
        {
            if (!names.Add(key) || !IsValidName(key)) errors.Add($"Invalid or duplicate question group: {key}.");
            if (!Keys.Contains(key, StringComparer.OrdinalIgnoreCase) &&
                ItemLabels.Values.Contains(key, StringComparer.OrdinalIgnoreCase))
                errors.Add($"{key}: this name is reserved for a built-in item type.");
            if (definition?.Questions is null)
            {
                errors.Add($"Missing question set: {key}.");
                continue;
            }
            if (SharedItemQuestions.HasProtectedQuestions(key))
            {
                var requiredIds = DynamicQuestionLoader.LoadDefaultGroup(key).Questions.Select(q => q.Id);
                foreach (var id in requiredIds)
                    if (!definition.Questions.Any(q => q != null && string.Equals(q.Id, id, StringComparison.OrdinalIgnoreCase)))
                        errors.Add($"{key}: shared question '{id}' cannot be deleted or renamed.");
            }
            var effective = SharedItemQuestions.Compose(key, definition, package);
            var validationQuestions = effective.Questions;
            if (IsItemSet(key))
            {
                var sharedIds = SharedItemQuestions.SharedIds(package);
                if (definition.Questions.Any(q => q != null && sharedIds.Contains(q.Id)))
                    errors.Add($"{key}: edit standard questions in Header or Footer instead of duplicating them in an item group.");
            }
            var workflows = definition.Questions.Where(q => q != null && DynamicWorkflowQuestions.IsWorkflow(q.Type));
            if (workflows.Any(q => !SupportsWorkflow(key, q.Type)))
                errors.Add($"{key}: this workflow type requires an assembly or flexible item group; Handles is available on any survey item.");
            foreach (var duplicate in workflows.GroupBy(q => q.Type).Where(g => g.Count() > 1))
                errors.Add($"{key}: use only one {duplicate.Key} question per item group.");
            if (validationQuestions.Count > 500)
                errors.Add($"{key}: maximum 500 questions.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allIds = validationQuestions.Where(q => q != null)
                .Select(q => q.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (key.Equals("Footer", StringComparison.OrdinalIgnoreCase) && package.Sets.TryGetValue("Header", out var header) && header?.Questions != null)
            {
                foreach (var q in header.Questions.Where(q => q != null)) { ids.Add(q.Id); allIds.Add(q.Id); }
            }
            foreach (var question in validationQuestions)
            {
                if (question is null) { errors.Add($"{key}: null question."); continue; }
                if (string.IsNullOrWhiteSpace(question.Id) || question.Id.Length > 100 ||
                    !System.Text.RegularExpressions.Regex.IsMatch(question.Id, "^[A-Za-z0-9_-]+$"))
                    errors.Add($"{key}: question IDs must be 1-100 letters, numbers, _ or -.");
                if (!ids.Add(question.Id ?? ""))
                    errors.Add($"{key}: duplicate ID '{question.Id}'.");
                if (string.IsNullOrWhiteSpace(question.Question) || question.Question.Length > 500)
                    errors.Add($"{key}/{question.Id}: enter question text (up to 500 characters).");
                if (!Enum.IsDefined(question.Type))
                    errors.Add($"{key}/{question.Id}: unknown question type.");
                if (question.Options is null || question.Options.Count > 100 ||
                    question.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200))
                    errors.Add($"{key}/{question.Id}: invalid options.");
                if (question.OptionRules is null || question.OptionRules.Count > 50)
                    errors.Add($"{key}/{question.Id}: maximum 50 option rules.");
                else
                {
                    foreach (var rule in question.OptionRules)
                    {
                        if (rule is null || rule.Options is null || rule.Options.Count > 100 ||
                            rule.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 200) ||
                            rule.Conditions is null || rule.Conditions.Count == 0 || rule.Conditions.Count > 10)
                        {
                            errors.Add($"{key}/{question.Id}: invalid conditional options.");
                            continue;
                        }
                        foreach (var optionCondition in rule.Conditions)
                        {
                            if (optionCondition is null || string.IsNullOrEmpty(optionCondition.QuestionId) ||
                                (!optionCondition.QuestionId.StartsWith('$') &&
                                 !allIds.Contains(optionCondition.QuestionId)) ||
                                string.Equals(
                                    optionCondition.QuestionId,
                                    question.Id,
                                    StringComparison.OrdinalIgnoreCase) ||
                                !Enum.IsDefined(optionCondition.Operator) ||
                                (optionCondition.Value?.Length ?? 0) > 200 ||
                                (optionCondition.QuestionId.StartsWith('$') &&
                                 !AllowedContexts.Contains(optionCondition.QuestionId)))
                            {
                                errors.Add(
                                    $"{key}/{question.Id}: option condition must reference an existing question or allowed context.");
                            }
                        }
                    }
                }
                // Image filenames are assets, never URLs or path components.
                static bool SafeImage(string? name) => !string.IsNullOrWhiteSpace(name) &&
                    System.Text.RegularExpressions.Regex.IsMatch(name,
                        @"^[A-Za-z0-9_-]+\.(png|jpg|jpeg|webp)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (question.Type == DynamicQuestionType.Image)
                {
                    if (!string.IsNullOrEmpty(question.Image) && !SafeImage(question.Image))
                        errors.Add($"{key}/{question.Id}: invalid image filename.");
                    if (question.ImageRules is null || question.ImageRules.Count > 80)
                        errors.Add($"{key}/{question.Id}: too many image rules.");
                    else foreach (var rule in question.ImageRules)
                    {
                        if (rule?.Conditions == null || rule.Conditions.Count == 0 ||
                            rule.Conditions.Count > 10 || rule.Images == null ||
                            rule.Images.Count == 0 || rule.Images.Count > 16 ||
                            rule.Images.Any(image => !SafeImage(image)))
                        {
                            errors.Add($"{key}/{question.Id}: invalid image rule.");
                            continue;
                        }
                        foreach (var c in rule.Conditions)
                        {
                            if (c == null || string.IsNullOrEmpty(c.QuestionId) ||
                                (!c.QuestionId.StartsWith('$') && !allIds.Contains(c.QuestionId)) ||
                                (c.QuestionId.StartsWith('$') && !AllowedContexts.Contains(c.QuestionId)) ||
                                !Enum.IsDefined(c.Operator) || (c.Value?.Length ?? 0) > 200)
                                errors.Add($"{key}/{question.Id}: invalid image condition.");
                        }
                    }
                }
                if ((question.Unit?.Length ?? 0) > 20 ||
                    question.Minimum.HasValue && !double.IsFinite(question.Minimum.Value) ||
                    question.Maximum.HasValue && !double.IsFinite(question.Maximum.Value) ||
                    question.Minimum.HasValue && question.Maximum.HasValue && question.Minimum > question.Maximum)
                    errors.Add($"{key}/{question.Id}: invalid measurement units or bounds.");
                if (question.Type == DynamicQuestionType.PlusMinus &&
                    (question.Minimum.HasValue && (question.Minimum < int.MinValue || question.Minimum > int.MaxValue || question.Minimum != Math.Truncate(question.Minimum.Value)) ||
                     question.Maximum.HasValue && (question.Maximum < int.MinValue || question.Maximum > int.MaxValue || question.Maximum != Math.Truncate(question.Maximum.Value)) ||
                     (question.Minimum ?? 0) > (question.Maximum ?? 10) ||
                     question.DefaultValue.HasValue && (question.DefaultValue < (question.Minimum ?? 0) || question.DefaultValue > (question.Maximum ?? 10))))
                    errors.Add($"{key}/{question.Id}: Plus Minus requires whole-number bounds and an initial value within the range.");
                if (question.ShowWhenAll == null || question.ShowWhenAll.Count > 10)
                    errors.Add($"{key}/{question.Id}: maximum 10 additional visibility conditions.");
                else foreach (var c in question.ShowWhenAll)
                {
                    if (c == null || string.IsNullOrEmpty(c.QuestionId) ||
                        (!ids.Contains(c.QuestionId) && !(c.QuestionId.StartsWith('$') && AllowedContexts.Contains(c.QuestionId))) ||
                        string.Equals(c.QuestionId, question.Id, StringComparison.OrdinalIgnoreCase) ||
                        !Enum.IsDefined(c.Operator) || (c.Value?.Length ?? 0) > 200)
                        errors.Add($"{key}/{question.Id}: additional conditions must reference earlier questions or allowed context.");
                }
                // Empty fallback options are valid where conditional rules supply them.
                if (question.ShowWhen is { } condition)
                {
                    if (string.IsNullOrEmpty(condition.QuestionId) || (!ids.Contains(condition.QuestionId) &&
                         !(condition.QuestionId.StartsWith('$') && AllowedContexts.Contains(condition.QuestionId))) ||
                        string.Equals(condition.QuestionId, question.Id, StringComparison.OrdinalIgnoreCase))
                        errors.Add($"{key}/{question.Id}: condition must reference an earlier question.");
                    if (!Enum.IsDefined(condition.Operator) || (condition.Value?.Length ?? 0) > 200)
                        errors.Add($"{key}/{question.Id}: invalid condition.");
                }
            }
        }

        return errors;
    }
}
