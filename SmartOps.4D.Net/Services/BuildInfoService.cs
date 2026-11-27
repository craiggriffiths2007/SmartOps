namespace SmartOps.Web.Services;

public class BuildInfoService
{
    public DateTime? PublishedUtc { get; }

    public BuildInfoService(IWebHostEnvironment environment)
    {
        var path = Path.Combine(
            environment.ContentRootPath,
            "publish-date.txt");

        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path).Trim();

        if (DateTime.TryParse(
                text,
                null,
                System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var date))
        {
            PublishedUtc = date;
        }
    }
}