using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("MetaNotes")]
    public class MetaNote
    {
        [Key]
        public int MetaNoteID { get; set; }

        public int ParagraphID { get; set; }

        [Required]
        [StringLength(50)]
        public string UniqueID { get; set; }

        [Required(AllowEmptyStrings = true)]
        public string MetaText { get; set; }

        [ForeignKey("ParagraphID")]
        public virtual Paragraph Paragraph { get; set; }
    }
}
