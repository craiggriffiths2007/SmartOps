using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels;

public sealed class SignedItemChangeViewModel : SignedMandateViewModel
{
    public CustomItem Item { get; set; } = null!;
    public string SignerName => string.IsNullOrWhiteSpace(Item.print_name) ? JobHeader.CustomerName : Item.print_name;
}
