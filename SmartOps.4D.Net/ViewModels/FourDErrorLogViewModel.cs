using System.Text.Json;

namespace SmartOps.Web.ViewModels;

public sealed class FourDErrorLogViewModel
{
    public List<FourDErrorLogEntry> Entries { get; set; } = [];
    public string? LoadError { get; set; }
}

public sealed class FourDErrorLogEntry
{
    public string Id { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public int Status { get; set; }
    public string Method { get; set; } = "";
    public string Path { get; set; } = "";
    public string Stage { get; set; } = "";
    public JsonElement Errors { get; set; }
    public string UkTime => TimeZoneInfo.ConvertTime(Timestamp,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/London")).ToString("dd/MM/yyyy HH:mm:ss zzz");
}
