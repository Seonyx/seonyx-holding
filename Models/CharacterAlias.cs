using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("CharacterAliases")]
    public class CharacterAlias
    {
        [Key]
        [StringLength(40)]
        public string CharacterAliasId { get; set; }

        [Required]
        [StringLength(40)]
        public string CharacterId { get; set; }

        [Required]
        [StringLength(200)]
        public string Alias { get; set; }

        [StringLength(40)]
        public string AliasTypeCode { get; set; }

        public int? SortOrder { get; set; }

        public string Notes { get; set; }

        // Navigation
        public virtual Character Character { get; set; }
    }
}
