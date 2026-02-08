using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Pages")]
    public class Page
    {
        [Key]
        public int PageId { get; set; }

        [Required]
        [StringLength(200)]
        [Index(IsUnique = true)]
        public string Slug { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; }

        [StringLength(500)]
        public string MetaDescription { get; set; }

        [StringLength(500)]
        public string MetaKeywords { get; set; }

        [Required]
        public string Content { get; set; }

        public int? ParentPageId { get; set; }

        public int? DivisionId { get; set; }

        public int SortOrder { get; set; }

        public bool IsPublished { get; set; }

        public bool ShowInNavigation { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime ModifiedDate { get; set; }

        [ForeignKey("ParentPageId")]
        public virtual Page ParentPage { get; set; }

        [ForeignKey("DivisionId")]
        public virtual Division Division { get; set; }

        public virtual ICollection<Page> ChildPages { get; set; }

        public Page()
        {
            IsPublished = true;
            ShowInNavigation = true;
            CreatedDate = DateTime.Now;
            ModifiedDate = DateTime.Now;
            ChildPages = new HashSet<Page>();
        }
    }
}
