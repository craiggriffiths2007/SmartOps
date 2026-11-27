using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{
    public class SurveyAppRequest
    {
        public int Id { get; set; }

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string TenantCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string AgentCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CurrentVersion { get; set; } = string.Empty;

        [MaxLength(50)]
        public string CurrentBuild { get; set; } = string.Empty;

        [MaxLength(50)]
        public string LatestBuild { get; set; } = string.Empty;

        public bool UpdateAvailable { get; set; }

        [MaxLength(100)]
        public string? DeviceManufacturer { get; set; }

        [MaxLength(100)]
        public string? DeviceModel { get; set; }

        [MaxLength(100)]
        public string? DeviceName { get; set; }

        [MaxLength(50)]
        public string? Platform { get; set; }

        [MaxLength(100)]
        public string? OsVersion { get; set; }

        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public class SurveyAppVersionRequestDto
    {
        public string TenantCode { get; set; } = string.Empty;
        public string AgentCode { get; set; } = string.Empty;
        public string CurrentVersion { get; set; } = string.Empty;
        public string CurrentBuild { get; set; } = string.Empty;
        public string AuthenticationString { get; set; } = string.Empty;
        public string? DeviceManufacturer { get; set; }
        public string? DeviceModel { get; set; }
        public string? DeviceName { get; set; }
        public string? Platform { get; set; }
        public string? OsVersion { get; set; }
    }

    public class SurveyAppVersionResponseDto
    {
        public string LatestBuild { get; set; } = string.Empty;
        public bool UpdateAvailable { get; set; }
    }
}
