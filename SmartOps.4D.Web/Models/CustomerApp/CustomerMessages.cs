using SmartOps.Web.Data;
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{
    public class SupportTicket : TenantEntity
    {
        public int Id { get; set; }

        public int ContractId { get; set; }
        public Contract? Contract { get; set; }

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = "";

        public TicketStatus Status { get; set; } = TicketStatus.Open;

        public DateTime Created { get; set; } = DateTime.UtcNow;

        public DateTime? Closed { get; set; }

        public List<SupportTicketMessage> Messages { get; set; } = [];
    }

    public enum TicketStatus
    {
        Open,
        AwaitingCustomer,
        AwaitingOffice,
        Closed
    }

    public class SupportTicketMessage : TenantEntity
    {
        public int Id { get; set; }

        public int SupportTicketId { get; set; }
        public SupportTicket SupportTicket { get; set; } = null!;

        public string Message { get; set; } = "";

        public TicketMessageSender Sender { get; set; }

        public DateTime Created { get; set; }

        public bool ReadByCustomer { get; set; }
        public bool ReadByOffice { get; set; }

        public string? SentByUserId { get; set; }
        public AppUser? SentByUser { get; set; }
    }

    public enum TicketMessageSender
    {
        Customer,
        HeadOffice
    }





    public class CustomerCreateTicketDTO
    {
        public string AuthenticationString { get; set; } = "";

        public int ContractId { get; set; }

        public string Subject { get; set; } = "";

        public string Message { get; set; } = "";
    }

    public class CustomerTicketRequestDTO
    {
        public string AuthenticationString { get; set; } = "";

        public int TicketId { get; set; }
    }

    public class CustomerTicketReplyDTO
    {
        public string AuthenticationString { get; set; } = "";

        public int TicketId { get; set; }

        public string Message { get; set; } = "";
    }

    public class CustomerTicketListDTO
    {
        public int Id { get; set; }

        public string Subject { get; set; } = "";

        public TicketStatus Status { get; set; }

        public DateTime Created { get; set; }

        public DateTime? LastMessageDate { get; set; }

        public int MessageCount { get; set; }

        public bool HasUnreadMessages { get; set; }
    }

    public class CustomerTicketDTO
    {
        public int Id { get; set; }

        public int ContractId { get; set; }

        public string Subject { get; set; } = "";

        public TicketStatus Status { get; set; }

        public DateTime Created { get; set; }

        public List<CustomerTicketMessageDTO> Messages { get; set; } = [];
    }

    public class CustomerTicketMessageDTO
    {
        public int Id { get; set; }

        public string Message { get; set; } = "";

        public TicketMessageSender Sender { get; set; }

        public string SenderName { get; set; } = "";

        public DateTime Created { get; set; }
    }



}