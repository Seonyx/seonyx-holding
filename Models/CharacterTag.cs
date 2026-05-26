using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("CharacterTags")]
    public class CharacterTag
    {
        [Key]
        [StringLength(40)]
        public string CharacterTagId { get; set; }

        [Required]
        [StringLength(40)]
        public string CharacterId { get; set; }

        [Required]
        [StringLength(100)]
        public string Tag { get; set; }

        // Navigation
        public virtual Character Character { get; set; }
    }
}
