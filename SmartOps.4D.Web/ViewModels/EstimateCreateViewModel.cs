using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.ViewModels;

public class EstimateCreateViewModel : IValidatableObject
{
    public int ContractId { get; set; }
    public string? ContractCode { get; set; }
    [Required, StringLength(200), Display(Name = "Service level")]
    public string ServiceLevel { get; set; } = "";
    [Range(typeof(decimal), "0", "100"), Display(Name = "VAT rate (%)")]
    public decimal VatRate { get; set; } = 20m;
    public List<EstimateLineInput> Lines { get; set; } = new() { new() };

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Lines == null || Lines.Count == 0 || Lines.Count > 100)
            yield return new ValidationResult("Add between 1 and 100 estimate lines.", new[] { nameof(Lines) });
        if (decimal.Round(VatRate, 2) != VatRate)
            yield return new ValidationResult("Enter a VAT rate with up to two decimal places.", new[] { nameof(VatRate) });
    }
}

public class EstimateLineInput : IValidatableObject
{
    [Required, StringLength(2000)]
    public string Details { get; set; } = "";
    [Range(typeof(decimal), "0", "999999999.99")]
    public decimal Cost { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (decimal.Round(Cost, 2) != Cost)
            yield return new ValidationResult("Enter a cost with up to two decimal places.", new[] { nameof(Cost) });
    }
}
