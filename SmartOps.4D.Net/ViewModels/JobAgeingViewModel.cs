using SmartOps.Web.Services;

namespace SmartOps.Web.ViewModels;

public sealed class JobAgeingViewModel
{
    public List<JobAgeingRow> Rows { get; set; } = [];
    public int Page { get; set; }
    public int TotalPages { get; set; }
}

public sealed record JobAgeingRow(
    int ContractId, string ContractNumber, int CustomerId,
    string CustomerName, string Status, string ActionRequired,
    string ActionController, string ActionName, string ActionParameter, string? ActionTab,
    ContractAgeingService.Health Health)
{
    public string Age => Health.Days is int days ? (days == 1 ? "1 day" : $"{days} days") : "Unknown";
}
