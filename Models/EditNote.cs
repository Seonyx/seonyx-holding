using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("EditNotes")]
    public class EditNote
    {
        [Key]
        public int EditNoteID { get; set; }

        public int ParagraphID { get; set; }

        [Required]
        [StringLength(50)]
        public string UniqueID { get; set; }

        public string NoteText { get; set; }

        public DateTime LastModifiedDate { get; set; }

        [ForeignKey("ParagraphID")]
        public virtual Paragraph Paragraph { get; set; }

        public EditNote()
        {
            LastModifiedDate = DateTime.Now;
        }
    }
}
