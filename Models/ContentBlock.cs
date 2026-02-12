using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("ContentBlocks")]
    public class ContentBlock
    {
        [Key]
        public int BlockId { get; set; }

        [Required]
        [StringLength(100)]
        [Index(IsUnique = true)]
        public string BlockKey { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public bool IsActive { get; set; }

        public DateTime ModifiedDate { get; set; }

        public ContentBlock()
        {
            IsActive = true;
            ModifiedDate = DateTime.Now;
        }
    }
}
