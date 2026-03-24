using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Chapters")]
    public class Chapter
    {
        [Key]
        public int ChapterID { get; set; }

        public int BookProjectID { get; set; }

        public int ChapterNumber { get; set; }

        /// <summary>Sort position within the book. Set from the component's index in book.xml.
        /// Numbered chapters have SortOrder == ChapterNumber; unnumbered components (epilogue, etc.)
        /// get a position after all numbered chapters.</summary>
        public int SortOrder { get; set; }

        [StringLength(500)]
        public string ChapterTitle { get; set; }

        [StringLength(255)]
        public string POV { get; set; }

        [StringLength(500)]
        public string Setting { get; set; }

        public string ChapterPurpose { get; set; }

        [StringLength(255)]
        public string SourceFileName { get; set; }

        /// <summary>BookML component id (e.g. 'ch01'). Null for chapters not imported via BookML.</summary>
        [StringLength(50)]
        public string BookmlChapterId { get; set; }

        [ForeignKey("BookProjectID")]
        public virtual BookProject BookProject { get; set; }

        public virtual ICollection<Paragraph> Paragraphs { get; set; }

        public Chapter()
        {
            Paragraphs = new HashSet<Paragraph>();
        }
    }
}
