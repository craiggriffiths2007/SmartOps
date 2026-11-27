using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{
    public class AgentMessage : TenantEntity
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Subject { get; set; } = "";

        [Required]
        public string BodyHtml { get; set; } = "";

        public DateTime Created { get; set; } = DateTime.UtcNow;

        public string CreatedBy { get; set; } = "";

        public List<AgentMessageRecipient> Recipients { get; set; } = new();
    }

    public class AgentMessageRecipient : TenantEntity
    {
        public int Id { get; set; }
        public int AgentMessageId { get; set; }
        public AgentMessage AgentMessage { get; set; } = null!;

        public int AgentId { get; set; }
        public Agent Agent { get; set; } = null!;

        public bool IsRead { get; set; }
        public DateTime? ReadDateTime { get; set; }
    }

    public class AgentMessageDTO
    {
        public int Id { get; set; }
        public string Subject { get; set; } = "";
        public string BodyHtml { get; set; } = "";
        public DateTime Created { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadDateTime { get; set; }
    }

    public class MarkAgentMessageReadDTO : GetDataDTO
    {
        public int MessageId { get; set; }
    }
}
