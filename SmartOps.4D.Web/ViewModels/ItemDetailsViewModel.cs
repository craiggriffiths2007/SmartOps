using Microsoft.EntityFrameworkCore;
using SmartOps.Web.Models;

namespace SmartOps.Web.ViewModels
{
    public class ItemDetailsViewModel
    {
        public CustomItem? CustomItem { get; set; }
        public List<CustomItem> CustomChildren { get; set; } = [];

        public IEnumerable<PhotoImage>? Images { get; set; }

    }
}
