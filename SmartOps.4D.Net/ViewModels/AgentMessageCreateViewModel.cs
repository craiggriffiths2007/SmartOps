using System.ComponentModel.DataAnnotations;
using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class AgentMessageCreateViewModel
    {
        [Required, StringLength(200)]
        public string Subject { get; set; } = "";

        [Required]
        public string BodyHtml { get; set; } = "";

        public List<int> SelectedAgentIds { get; set; } = new();
        public List<Agent> Agents { get; set; } = new();
    }
}
