using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models;

public class BookingReservation : TenantEntity
{
    public Guid Id { get; set; }
    [MaxLength(450)]
    public string UserId { get; set; } = "";
    public int AgentId { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public DateTime ExpiresUtc { get; set; }
}
