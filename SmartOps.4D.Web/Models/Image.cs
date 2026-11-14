using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Policy;

namespace SmartOps.Web.Models
{
    [Index(nameof(Filename))]
    public class PhotoImage : TenantEntity
    {
        [Key]
        public int Id { get; set; }
        public string Filename { get; set; } = "";

        public string Data { get; set; } = "";
        public DateTime DateTime { get; set; }
        public string? ContractCode { get; set; }
        public int HeaderId { get; set; }

        public bool CustomerVisible { get; set; } = true;
    }

    public class ImageDTO
    {
        public string TenantCode { get; set; } = "";
        public string AgentCode { get; set; } = "";

        public string AuthenticationString { get; set; } = "";
        public string Filename { get; set; } = "";
        public string Data { get; set; } = "";
        // optional chunking metadata
        public int? ChunkIndex { get; set; }
        public int? TotalChunks { get; set; }

    }

}
