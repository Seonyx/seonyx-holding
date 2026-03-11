using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Paragraphs")]
    public class Paragraph
    {
        [Key]
        public int ParagraphID { get; set; }

        public int ChapterID { get; set; }

        [Required]
        [StringLength(50)]
        public string UniqueID { get; set; }

        public int OrdinalPosition { get; set; }

        [Required(AllowEmptyStrings = true)]
        public string ParagraphText { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime LastModifiedDate { get; set; }

        [ForeignKey("ChapterID")]
        public virtual Chapter Chapter { get; set; }

        public Paragraph()
        {
            CreatedDate = DateTime.Now;
            LastModifiedDate = DateTime.Now;
        }
    }
}
