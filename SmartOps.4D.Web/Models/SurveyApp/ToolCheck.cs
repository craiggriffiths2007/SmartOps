using SmartOps.Shared.Models;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{
    public class ToolCheck : ToolCheckBase
    {
        [Browsable(false)]
        [Key]
        public int Id { get; set; }
    }
}
