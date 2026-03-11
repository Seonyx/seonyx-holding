using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    /// <summary>
    /// Append-only snapshot record for one paragraph at one draft number.
    /// Never update or delete rows. Join is always on Pid — never on Seq.
    /// </summary>
    [Table("ParagraphVersions")]
    public class ParagraphVersion
    {
        [Key]
        public int VersionID { get; set; }

        /// <summary>Immutable BookML paragraph identity key, e.g. CH01-P0010.</summary>
        [Required]
        [StringLength(50)]
        public string Pid { get; set; }

        public int ChapterID { get; set; }

        public int DraftNumber { get; set; }

        /// <summary>Display ordinal at time of snapshot. Mutable — never use as a foreign key.</summary>
        public int Seq { get; set; }

        [Required]
        [StringLength(20)]
        public string ParaType { get; set; }

        [Required(AllowEmptyStrings = true)]
        public string Content { get; set; }

        /// <summary>Draft number in which this pid was first introduced.</summary>
        public int DraftCreated { get; set; }

        /// <summary>Draft number in which content was last changed.</summary>
        public int DraftModified { get; set; }

        /// <summary>human or ai</summary>
        [Required]
        [StringLength(10)]
        public string ModifiedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        /// <summary>created | modified | moved | deleted | unchanged</summary>
        [StringLength(20)]
        public string ChangeType { get; set; }

        [ForeignKey("ChapterID")]
        public virtual Chapter Chapter { get; set; }

        public ParagraphVersion()
        {
            ParaType = "normal";
        }
    }
}
