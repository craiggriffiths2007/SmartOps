using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class MessagingViewModel
    {
        public List<SupportTicket> CustomerMessages { get; set; } = new();

        public List<InsurerMessage> InsurerMessages { get; set; } = new();
    }
}