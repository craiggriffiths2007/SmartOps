using System.Text.Json;

namespace SmartOps.Shared.Models;

/// <summary>
/// Common serialisation for saved questionnaire answers and visit-owned snapshots.
/// Existing JSON columns and version fields are unchanged. A saved definition is
/// never silently replaced with the tenant's newest published definition.
/// </summary>
public static class DynamicQuestionDocuments
{
    public static DynamicQuestionDefinition? ReadSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<DynamicQuestionDefinition>(
                json, DynamicQuestionLoader.JsonOptions);
        }
        catch (JsonException) { return null; }
    }

    public static List<DynamicAnswer> ReadAnswers(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<DynamicAnswer>>(
                json, DynamicQuestionLoader.JsonOptions) ?? [];
        }
        catch (JsonException) { return []; }
    }

    public static string WriteSnapshot(DynamicQuestionDefinition definition) =>
        JsonSerializer.Serialize(definition, DynamicQuestionLoader.JsonOptions);

    public static string WriteAnswers(IEnumerable<DynamicAnswer> answers) =>
        JsonSerializer.Serialize(answers, DynamicQuestionLoader.JsonOptions);

    // A call site can use this to capture a visit's first definition but should
    // keep its existing version column (the caller owns that business decision).
    public static string CaptureSnapshotIfMissing(string? existing,
        DynamicQuestionDefinition definition) =>
        ReadSnapshot(existing) is null ? WriteSnapshot(definition) : existing!;
}
