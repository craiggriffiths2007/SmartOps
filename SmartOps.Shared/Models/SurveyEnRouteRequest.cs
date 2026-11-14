namespace SmartOps.Shared.Models;

public sealed class SurveyEnRouteRequest
{
    public string TenantCode { get; set; } = "";
    public string AgentCode { get; set; } = "";
    public string AuthenticationString { get; set; } = "";
    public string ContractCode { get; set; } = "";
    public DateTime DiaryDate { get; set; }
    public bool Resend { get; set; }
    public DateTime? PressedAtUtc { get; set; }
    public Guid HeaderGuid { get; set; }
}

public sealed class SurveyEnRouteResult
{
    public DateTime? EnRoutePressedAtUtc { get; set; }
}
