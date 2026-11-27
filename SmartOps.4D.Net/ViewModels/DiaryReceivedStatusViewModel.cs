using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class DiaryReceivedStatusViewModel
    {
        public ContractStatus? Status { get; set; }

        public IEnumerable<JobHeader> Headers { get; set; } = Enumerable.Empty<JobHeader>();
    }
}
