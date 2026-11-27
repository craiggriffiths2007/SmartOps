using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class ContractIndexViewModel
    {
        public Contract Contract { get; set; } = null!;

        public Job? NextJob { get; set; }

        public List<JobHeader> Headers { get; set; } = new();
    }
}