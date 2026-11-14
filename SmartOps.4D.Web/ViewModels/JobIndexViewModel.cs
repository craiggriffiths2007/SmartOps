using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class JobIndexViewModel
    {
        public IEnumerable<Job>? Jobs { get; set; }
        public List<JobHeader> Headers { get; set; } = new();
        public HashSet<string> ContractCodesWithHeaders { get; set; } = new();
        public int Year { get; set; }
        public int Month { get; set; }
        public string ViewMode { get; set; } = "month";
        public DateTime WeekStartDate { get; set; }
        public string? SearchQuery { get; set; }
        public int? BranchId { get; set; }
        public List<Branch> Branches { get; set; } = new();
        // For the All view: a date range to display
        public DateTime AllStartDate { get; set; }
        public DateTime AllEndDate { get; set; }
        // Pagination for list (All) view
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}
