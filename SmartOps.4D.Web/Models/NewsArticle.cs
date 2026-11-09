using System.ComponentModel.DataAnnotations;

namespace SmartOps.Web.Models
{
    public class NewsArticle
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "";

        [Required]
        [StringLength(500)]
        public string Summary { get; set; } = "";

        [Required]
        public string BodyHtml { get; set; } = "";

        [Display(Name = "Publish date")]
        public DateTime PublishedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Published")]
        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
