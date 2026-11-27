using System.Globalization;

namespace SmartOps.Shared.Models;

public static class PlusMinusQuestions
{
    public static int Minimum(DynamicQuestion q) => (int)Math.Clamp(Math.Ceiling(q.Minimum ?? 0), int.MinValue, int.MaxValue);
    public static int Maximum(DynamicQuestion q) => (int)Math.Clamp(Math.Floor(q.Maximum ?? 10), Minimum(q), int.MaxValue);
    public static int InitialValue(DynamicQuestion q, string? value) => Math.Clamp(
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int saved) ? saved : q.DefaultValue ?? Minimum(q), Minimum(q), Maximum(q));
    public static bool IsValid(DynamicQuestion q, string? value) => string.IsNullOrWhiteSpace(value) ? !q.Required
        : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) && number >= Minimum(q) && number <= Maximum(q);
}
