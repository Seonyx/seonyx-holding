using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Authors")]
    public class Author
    {
        [Key]
        public int AuthorId { get; set; }

        [Required]
        [StringLength(200)]
        public string PenName { get; set; }

        public string Biography { get; set; }

        [StringLength(500)]
        public string PhotoUrl { get; set; }

        [StringLength(200)]
        public string Genre { get; set; }

        [StringLength(500)]
        public string Website { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public virtual ICollection<Book> Books { get; set; }

        public Author()
        {
            IsActive = true;
            CreatedDate = DateTime.Now;
            Books = new HashSet<Book>();
        }
    }
}
