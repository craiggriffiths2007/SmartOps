using System.ComponentModel;

namespace SmartOps.Web.Models
{
    public class Branch : TenantEntity
    {
        public int Id { get; set; }
        [DisplayName("Location")]
        public string? Name { get; set; } = null;
        [DisplayName("Code")]
        public string? Code { get; set; } = null;
        [DisplayName("Description")]
        public string? Description { get; set; } = null;


    }
}
