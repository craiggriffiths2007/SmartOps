using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SmartOps.Web.Models;

public enum InvoiceBillingParty { Customer, Insurer }

public class Invoice : TenantEntity
{
    public int Id { get; set; }
    [NotMapped]
    public string InvoiceNumber => $"INV-{Id:D8}";
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int? EstimateId { get; set; }
    public Estimate? Estimate { get; set; }
    public string ContractCode { get; set; } = "";
    public string PolicyNumber { get; set; } = "";
    public InvoiceBillingParty BillingParty { get; set; }
    public string BillToName { get; set; } = "";
    public string BillToAddress { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string ServiceAddress { get; set; } = "";
    public string SellerName { get; set; } = "";
    public string SellerAddress { get; set; } = "";
    public string SellerEmail { get; set; } = "";
    public string SellerTelephone { get; set; } = "";
    [StringLength(50)]
    public string VatRegistrationNumber { get; set; } = "";
    [Required, StringLength(200)]
    public string Description { get; set; } = "";
    [Column(TypeName = "date")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    [StringLength(256)]
    public string IssuedBy { get; set; } = "";
    [Precision(18, 2)]
    public decimal VatRate { get; set; }
    [Precision(18, 2)]
    public decimal Subtotal { get; private set; }
    [Precision(18, 2)]
    public decimal VatAmount { get; private set; }
    [Precision(18, 2)]
    public decimal Total { get; private set; }
    public List<InvoiceLine> Lines { get; set; } = new();

    public void CalculateTotals()
    {
        Subtotal = Lines.Sum(line => line.Cost);
        VatAmount = Math.Round(Subtotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
        Total = Subtotal + VatAmount;
    }
}

public class InvoiceLine : TenantEntity
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int SortOrder { get; set; }
    [Required, StringLength(2000)]
    public string Details { get; set; } = "";
    [Precision(18, 2)]
    public decimal Cost { get; set; }
}
