using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Divisions")]
    public class Division
    {
        [Key]
        public int DivisionId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(200)]
        [Index(IsUnique = true)]
        public string Slug { get; set; }

        public string Description { get; set; }

        [StringLength(500)]
        public string LogoUrl { get; set; }

        [StringLength(500)]
        public string WebsiteUrl { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        [StringLength(7)]
        public string BackgroundColor { get; set; }

        [StringLength(7)]
        public string ForegroundColor { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual ICollection<Page> Pages { get; set; }

        public Division()
        {
            IsActive = true;
            CreatedDate = DateTime.Now;
            Pages = new HashSet<Page>();
        }
    }
}
