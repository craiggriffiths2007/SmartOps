namespace SmartOps.Web.Models
{
    public class PageHit
    {
        public int Id { get; set; }

        public string Page { get; set; } = "";

        public DateTime HitDate { get; set; } = DateTime.UtcNow;

        public string? Referrer { get; set; }

        public string? IpAddress { get; set; }

        public string? UserAgent { get; set; }
    }
}