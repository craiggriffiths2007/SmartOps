namespace SmartOps.Web.Bridge;

/// <summary>
/// Explicit allow-list of legacy 4D survey endpoints. Never proxy all site routes:
/// the ASP.NET web app also owns /Account, /Home and /api endpoints.
/// </summary>
public static class SurveyProxyPaths
{
    public static readonly string[] PostPaths =
    [
        "/AgentLogin",
        "/GetJobs",
        "/GetDynamicQuestions",
        "/GetVehicles",
        "/SendJobs",
        "/SendMedia",
        "/GetImage",
        "/SendChecks",
        "/GetUpdateContractNotes",
        "/GetSpotCheckJobInfo",
        "/GetAgentMessages",
        "/GetAgentUnreadMessageCount",
        "/MarkAgentMessageRead",
        "/SurveyEnRoute",
        "/Home/SurveyAppVersion"
    ];

    public static bool IsSurveyPath(PathString path) =>
        string.Equals(path.Value, "/api/health", StringComparison.OrdinalIgnoreCase) ||
        PostPaths.Any(p => string.Equals(path.Value, p, StringComparison.OrdinalIgnoreCase));
}
