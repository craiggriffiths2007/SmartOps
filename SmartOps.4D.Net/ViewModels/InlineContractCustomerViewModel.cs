using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.ViewModels;

public class InlineContractCustomerViewModel
{
    [Required, Display(Name = "Name")]
    public string? Name { get; set; }

    [Required, Display(Name = "Address")]
    public string? Add1 { get; set; }

    [Display(Name = "Address 2")]
    public string? Add2 { get; set; }

    [Display(Name = "Address 3")]
    public string? Add3 { get; set; }

    [Required, Display(Name = "Postcode")]
    public string? Postcode { get; set; }

    [Display(Name = "Mobile")]
    public string? Phone1 { get; set; }

    [Display(Name = "Home")]
    public string? Phone2 { get; set; }

    [Display(Name = "Work")]
    public string? Phone3 { get; set; }

    [EmailAddress, Display(Name = "Email")]
    public string? Email { get; set; }
}