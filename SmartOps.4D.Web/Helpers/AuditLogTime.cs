using System.Globalization;

namespace SmartOps.Web.Helpers;

public static class AuditLogTime
{
    // Use the same UK zone on Windows and Linux, independently of server settings.
    private static readonly TimeZoneInfo Zone =
        TimeZoneInfo.TryFindSystemTimeZoneById("Europe/London", out var zone)
            ? zone : TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    public static string Format(DateTime utc)
    {
        // SQL datetime2 is read back with Kind.Unspecified, but audit timestamps are UTC.
        var instant = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
        var local = TimeZoneInfo.ConvertTime(instant, Zone);
        string abbreviation = Zone.IsDaylightSavingTime(instant) ? "BST" : "GMT";
        return local.ToString("dd MMM yyyy HH:mm:ss", CultureInfo.GetCultureInfo("en-GB")) + " " + abbreviation;
    }

    public static DateTime StartOfDayUtc(DateTime localDate) => TimeZoneInfo.ConvertTimeToUtc(
        DateTime.SpecifyKind(localDate.Date, DateTimeKind.Unspecified), Zone);
}
