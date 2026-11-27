using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.ViewModels;

public class InvoiceCreateViewModel : IValidatableObject
{
    public int ContractId { get; set; }
    public string? ContractCode { get; set; }
    public string? BillToName { get; set; }
    public string? BillToAddress { get; set; }
    public string? BillingPartyText { get; set; }
    public string? EstimateReference { get; set; }
    [Required, StringLength(200)]
    public string Description { get; set; } = "";
    [DataType(DataType.Date), Display(Name = "Invoice date")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    [StringLength(50), Display(Name = "Your VAT registration number")]
    public string? VatRegistrationNumber { get; set; }
    [Range(typeof(decimal), "0", "100"), Display(Name = "VAT rate (%)")]
    public decimal VatRate { get; set; } = 20m;
    public List<EstimateLineInput> Lines { get; set; } = new() { new() };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Lines == null || Lines.Count is < 1 or > 100)
            yield return new ValidationResult("Add between 1 and 100 invoice lines.", new[] { nameof(Lines) });
        if (decimal.Round(VatRate, 2) != VatRate)
            yield return new ValidationResult("Enter a VAT rate with up to two decimal places.", new[] { nameof(VatRate) });
    }
}
