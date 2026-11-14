namespace SmartOps.Web.Services
{
    public class EmailSettings
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; }
        public string SenderName { get; set; } = "";
        public string SenderEmail { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool UseSsl { get; set; }
    }
}