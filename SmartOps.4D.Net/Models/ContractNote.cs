using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{

    public class ContractNote : TenantEntity
    {
        [Key]
        public int Id { get; set; }

        [DisplayName("Contract Code")]
        public string? ContractCode { get; set; }

        [DisplayName("Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [DisplayName("Note")]
        public string Note { get; set; } = "";
        [DisplayName("Added By")]
        public string AddedBy { get; set; } = "";

    }
}
