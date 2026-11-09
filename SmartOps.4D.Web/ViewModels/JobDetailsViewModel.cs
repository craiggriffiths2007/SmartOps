using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class JobDetailsViewModel
    {
        public List<ContractUpdate> ContractUpdates { get; set; } = new();
        public List<JobHeaderIndex>? JobHeaderIndex { get; set; }

        public Job? Job { get; set; }
    }
}
