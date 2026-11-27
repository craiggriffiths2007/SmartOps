using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Models;

public class Estimate : TenantEntity
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    [Required, StringLength(200)]
    public string ServiceLevel { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    [StringLength(256)]
    public string CreatedBy { get; set; } = "";
    [Precision(18, 2)]
    public decimal VatRate { get; set; }
    [Precision(18, 2)]
    public decimal Subtotal { get; private set; }
    [Precision(18, 2)]
    public decimal VatAmount { get; private set; }
    [Precision(18, 2)]
    public decimal Total { get; private set; }
    [ConcurrencyCheck]
    public DateTime? AuthorizedUtc { get; set; }
    [StringLength(256)]
    public string? AuthorizedBy { get; set; }
    [NotMapped]
    public bool IsAuthorized => AuthorizedUtc.HasValue;
    public List<EstimateLine> Lines { get; set; } = new();

    public void CalculateTotals()
    {
        Subtotal = Lines.Sum(line => line.Cost);
        VatAmount = Math.Round(Subtotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
        Total = Subtotal + VatAmount;
    }
}

public class EstimateLine : TenantEntity
{
    public int Id { get; set; }
    public int EstimateId { get; set; }
    public Estimate Estimate { get; set; } = null!;
    public int SortOrder { get; set; }
    [Required, StringLength(2000)]
    public string Details { get; set; } = "";
    [Precision(18, 2)]
    public decimal Cost { get; set; }
}
