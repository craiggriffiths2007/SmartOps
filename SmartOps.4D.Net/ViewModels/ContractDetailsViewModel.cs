using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class ContractDetailsViewModel
    {
        public Contract? Contract { get; set; }

        public List<InsurerMessage> InsurerMessages { get; set; } = new();

        public string InsurerMessage { get; set; } = "";

        public IEnumerable<ContractNote> ContractNotes { get; set; } = new List<ContractNote>();

        public List<ContractUpdate> ContractUpdates { get; set; } = new();

        public List<SupportTicket> SupportTickets { get; set; } = [];
        public string Note { get; set; } = "";

        public Job? NextJob { get; set; }

        public List<Job> Bookings { get; set; } = new();

        public int? InvoiceId { get; set; }

        public List<Estimate> Estimates { get; set; } = new();

        public List<JobHeader> JobsReceived { get; set; } = new();
    }
}
