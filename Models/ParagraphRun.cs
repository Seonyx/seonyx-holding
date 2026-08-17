using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("ParagraphRuns")]
    public class ParagraphRun
    {
        [Key]
        public int ParagraphRunID { get; set; }

        public int ParagraphID { get; set; }

        public int Seq { get; set; }

        /// <summary>NULL only when IsBreak = true.</summary>
        public string RunText { get; set; }

        public bool IsEmph { get; set; }

        [StringLength(32)]
        public string EmphType { get; set; }

        public bool IsStrong { get; set; }

        public bool IsSmallCaps { get; set; }

        /// <summary>Non-null => this run is inside an overlay of this type.</summary>
        [StringLength(32)]
        public string OverlayType { get; set; }

        public bool IsBreak { get; set; }

        [ForeignKey("ParagraphID")]
        public virtual Paragraph Paragraph { get; set; }
    }
}
