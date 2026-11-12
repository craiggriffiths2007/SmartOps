using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class InsurerContractListItemViewModel
    {
        public Contract Contract { get; set; } = new();

        public int UnreadMessages { get; set; }
    }
}
