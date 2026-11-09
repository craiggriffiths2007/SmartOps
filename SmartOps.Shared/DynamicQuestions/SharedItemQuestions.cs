namespace SmartOps.Shared.Models;

/// <summary>Shared standard groups are stored once and composed around each item's questions.</summary>
public static class SharedItemQuestions
{
    public static IReadOnlyList<string> ProtectedGroups { get; } = ["Header", "Footer", HealthAndSafetyQuestions.Key, JobSummaryQuestions.Key, RepudiationQuestions.Key, GarageDoorMotorDeclarationQuestions.Key, "FitterCompletion", "FitterStockUsage", "RemedialCompletion"];
    public static bool HasProtectedQuestions(string key) => key.Equals("Header", StringComparison.OrdinalIgnoreCase)
        || key.Equals("Footer", StringComparison.OrdinalIgnoreCase)
        || key.Equals(VehicleAccidentQuestions.Key, StringComparison.OrdinalIgnoreCase)
        || key.Equals(SpotCheckQuestions.Key, StringComparison.OrdinalIgnoreCase);
    public static bool IsSharedGroup(string key) => ProtectedGroups.Contains(key, StringComparer.OrdinalIgnoreCase);

    public static HashSet<string> SharedIds(DynamicQuestionSetPackage package) =>
        new(new[] { "Header", "Footer" }
            .Where(package.Sets.ContainsKey)
            .SelectMany(key => package.Sets[key]?.Questions ?? [])
            .Where(q => q != null).Select(q => q.Id), StringComparer.OrdinalIgnoreCase);

    public static DynamicQuestionDefinition Compose(string key, DynamicQuestionDefinition definition,
        DynamicQuestionSetPackage? package = null)
    {
        var result = DynamicQuestionLoader.Copy(definition);
        if (!DynamicQuestionCatalogue.IsItemSet(key)) return result;
        DynamicQuestionDefinition Group(string name) => package is null
            ? DynamicQuestionLoader.LoadDefaultGroup(name)
            : package.Sets.GetValueOrDefault(name) ?? new();
        result.Questions = [
            .. (result.IncludeHeaderQuestions ? Group("Header").Questions ?? [] : []),
            .. (result.Questions ?? []),
            .. (result.IncludeFooterQuestions ? Group("Footer").Questions ?? [] : [])
        ];
        return DynamicQuestionLoader.Copy(result);
    }

    public static List<string> ValidateRetention(DynamicQuestionSetPackage previous, DynamicQuestionSetPackage next)
    {
        var errors = new List<string>();
        foreach (var key in ProtectedGroups.Where(HasProtectedQuestions))
        {
            if (!previous.Sets.TryGetValue(key, out var oldGroup)) continue;
            var retained = next.Sets.GetValueOrDefault(key)?.Questions?.Where(q => q != null)
                .Select(q => q.Id).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
            foreach (var q in oldGroup.Questions)
                if (!(key.Equals("Footer", StringComparison.OrdinalIgnoreCase) && q.Id == "print_name") && !retained.Contains(q.Id)) errors.Add($"{key}: shared question '{q.Id}' cannot be deleted or renamed.");
        }
        return errors;
    }
}
