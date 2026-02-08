using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Books")]
    public class Book
    {
        [Key]
        public int BookId { get; set; }

        public int AuthorId { get; set; }

        [Required]
        [StringLength(500)]
        public string Title { get; set; }

        public string Synopsis { get; set; }

        [StringLength(500)]
        public string CoverImageUrl { get; set; }

        [StringLength(500)]
        public string AmazonUrl { get; set; }

        [StringLength(500)]
        public string KDPUrl { get; set; }

        [StringLength(20)]
        public string ISBN { get; set; }

        public DateTime? PublicationDate { get; set; }

        [StringLength(200)]
        public string Genre { get; set; }

        public int SortOrder { get; set; }

        public bool IsPublished { get; set; }

        public DateTime CreatedDate { get; set; }

        [ForeignKey("AuthorId")]
        public virtual Author Author { get; set; }

        public Book()
        {
            CreatedDate = DateTime.Now;
        }
    }
}
