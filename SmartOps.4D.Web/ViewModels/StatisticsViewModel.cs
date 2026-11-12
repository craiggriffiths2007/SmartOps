using SmartOps.Web.Helpers;

namespace SmartOps.Web.ViewModels
{
    public class StatisticsViewModel
    {
        public int TotalHits { get; set; }
        public int VisitorHits { get; set; }
        public int BotHits { get; set; }
        public int PossibleBotHits { get; set; }
        public int UnknownAgentHits { get; set; }
        public int TodayHits { get; set; }
        public int Last7DaysHits { get; set; }
        public int Last30DaysHits { get; set; }
        public int UniqueIpAddresses { get; set; }
        public string? CurrentIpAddress { get; set; }

        public List<PageStatisticsViewModel> Pages { get; set; } = new();
        public List<DailyHitViewModel> DailyHits { get; set; } = new();
        public List<RecentPageHitViewModel> RecentHits { get; set; } = new();
        public int SurveyAppChecksToday { get; set; }
        public int SurveyAppChecksLast30Days { get; set; }
        public string SurveyAppLatestBuild { get; set; } = "";
        public List<RecentSurveyAppRequestViewModel> RecentSurveyAppRequests { get; set; } = new();
    }

    public class PageStatisticsViewModel
    {
        public string Page { get; set; } = "";
        public int Total { get; set; }
        public int Today { get; set; }
        public int Last7Days { get; set; }
        public int Last30Days { get; set; }
    }

    public class DailyHitViewModel
    {
        public DateTime Date { get; set; }
        public int Hits { get; set; }
    }

    public class RecentPageHitViewModel
    {
        public int Id { get; set; }
        public string Page { get; set; } = "";
        public DateTime HitDate { get; set; }
        public string? IpAddress { get; set; }
        public string? Referrer { get; set; }
        public string? UserAgent { get; set; }
        public bool IsCurrentIp { get; set; }
        public VisitorAgentInfo Visitor { get; set; } = new("Unknown", "Unknown", "Unknown", VisitorTrafficType.Unknown);
    }
    public class RecentSurveyAppRequestViewModel
    {
        public DateTime RequestDate { get; set; }
        public string TenantCode { get; set; } = "";
        public string AgentCode { get; set; } = "";
        public string CurrentVersion { get; set; } = "";
        public string CurrentBuild { get; set; } = "";
        public string LatestBuild { get; set; } = "";
        public bool UpdateAvailable { get; set; }
        public string? DeviceManufacturer { get; set; }
        public string? DeviceModel { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
        public string? OsVersion { get; set; }
        public string? IpAddress { get; set; }
    }

}
