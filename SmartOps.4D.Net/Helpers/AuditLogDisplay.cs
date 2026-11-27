using System.Text.Json;

namespace SmartOps.Web.Helpers;

public static class AuditLogDisplay
{
    public static string RecordId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return value;
            var visible = document.RootElement.EnumerateObject()
                .Where(p => !p.Name.Equals("TenantId", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(p => p.Name, p => p.Value);
            return visible.Count == 0 ? "" : JsonSerializer.Serialize(visible);
        }
        catch (JsonException) { return value; }
    }

    public static string ChangedFields(string? value) => string.Join(", ",
        (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(field => !field.Equals("TenantId", StringComparison.OrdinalIgnoreCase)));
}
