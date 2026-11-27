using System.Text.RegularExpressions;
using SmartOps.Web.Models;
using SmartOps.Web.ViewModels;
using UAParser;

namespace SmartOps.Web.Helpers;

public enum VisitorTrafficType { Visitor, Bot, PossibleBot, Unknown }

public sealed record VisitorAgentInfo(string Device, string Browser, string OperatingSystem,
    VisitorTrafficType TrafficType)
{
    public string VisitorType => TrafficType switch
    {
        VisitorTrafficType.Visitor => "Likely human",
        VisitorTrafficType.Bot => "Bot (unverified)",
        VisitorTrafficType.PossibleBot => "Possible bot",
        _ => "Unknown"
    };
}

/// <summary>Display-only interpretation; raw agents remain in PageHits. No DNS or network calls.</summary>
public static class VisitorAgentParser
{
    private static readonly Lazy<Parser> AgentParser = new(() => Parser.GetDefault());
    private static readonly Regex BotMarker = new(@"bot\b|crawler|spider|slurp|facebookexternalhit|facebookcatalog|archive\.org_bot",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static VisitorAgentInfo Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent) || userAgent.Length > 4096)
            return new("Unknown", "Unknown", "Unknown", VisitorTrafficType.Unknown);

        var agent = userAgent;
        var knownBot = new[] { "Googlebot", "bingbot", "SEOJuice-SearchBot", "DuckDuckBot", "YandexBot", "Baiduspider", "Applebot", "AhrefsBot", "SemrushBot" }
            .FirstOrDefault(name => agent.Contains(name, StringComparison.OrdinalIgnoreCase));
        var automated = new[] { "HeadlessChrome", "PhantomJS", "python-requests", "python-urllib", "curl/", "wget/", "Go-http-client", "okhttp/", "selenium", "playwright", "puppeteer", "httpclient", "lighthouse" }
            .Any(name => agent.Contains(name, StringComparison.OrdinalIgnoreCase));
        try
        {
            var client = AgentParser.Value.Parse(agent);
            bool bot = knownBot != null || BotMarker.IsMatch(agent) || client.Device.Family == "Spider";
            string browser = knownBot ?? Describe(client.UA.Family, client.UA.Major, client.UA.Minor, client.UA.Patch);
            string os = Describe(client.OS.Family, client.OS.Major, client.OS.Minor, client.OS.Patch);
            if (client.OS.Family == "Windows" && agent.Contains("Windows NT 10.0", StringComparison.OrdinalIgnoreCase))
                os = "Windows 10 / 11";

            var traffic = bot ? VisitorTrafficType.Bot : automated ? VisitorTrafficType.PossibleBot
                : client.UA.Family is not ("Other" or "") ? VisitorTrafficType.Visitor : VisitorTrafficType.Unknown;
            string device = Device(agent, client.Device.Family, client.OS.Family, traffic);
            return new(device, browser, os, traffic);
        }
        catch (RegexMatchTimeoutException)
        {
            return new("Unknown", knownBot ?? "Unknown", "Unknown",
                knownBot != null ? VisitorTrafficType.Bot : VisitorTrafficType.Unknown);
        }
    }

    private static string Describe(string family, params string?[] version)
    {
        if (string.IsNullOrWhiteSpace(family) || family == "Other") return "Unknown";
        var number = string.Join(".", version.TakeWhile(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(number) ? family : $"{family} {number}";
    }

    private static string Device(string agent, string family, string os, VisitorTrafficType traffic)
    {
        if (traffic == VisitorTrafficType.Bot) return "Bot";
        bool tablet = agent.Contains("iPad", StringComparison.OrdinalIgnoreCase)
            || agent.Contains("Tablet", StringComparison.OrdinalIgnoreCase)
            || agent.Contains("Kindle", StringComparison.OrdinalIgnoreCase)
            || (os == "Android" && !agent.Contains("Mobile", StringComparison.OrdinalIgnoreCase));
        bool mobile = agent.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
            || agent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) || os is "Android" or "iOS";
        string type = tablet ? "Tablet" : mobile ? "Mobile"
            : os is "Windows" or "Mac OS X" or "Linux" or "Chrome OS" or "Ubuntu" ? "Desktop" : "Unknown";
        return family is "Other" or "Generic Smartphone" or "Generic Tablet" or "Spider" or ""
            ? type : $"{family} ({type})";
    }

    public static void PopulateStatistics(StatisticsViewModel model, IEnumerable<PageHit> hits)
    {
        // Parse each distinct agent once per page render, without an unbounded global cache.
        var agents = new Dictionary<string, VisitorAgentInfo>(StringComparer.Ordinal);
        VisitorAgentInfo Resolve(string? agent)
        {
            string key = agent ?? "";
            if (!agents.TryGetValue(key, out var info)) agents[key] = info = Parse(agent);
            return info;
        }
        foreach (var group in hits.GroupBy(hit => hit.UserAgent ?? ""))
        {
            var count = group.Count();
            switch (Resolve(group.Key).TrafficType)
            {
                case VisitorTrafficType.Visitor: model.VisitorHits += count; break;
                case VisitorTrafficType.Bot: model.BotHits += count; break;
                case VisitorTrafficType.PossibleBot: model.PossibleBotHits += count; break;
                default: model.UnknownAgentHits += count; break;
            }
        }
        foreach (var hit in model.RecentHits) hit.Visitor = Resolve(hit.UserAgent);
    }
}
