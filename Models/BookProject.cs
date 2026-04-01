using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("BookProjects")]
    public class BookProject
    {
        [Key]
        public int BookProjectID { get; set; }

        [Required]
        [StringLength(255)]
        public string ProjectName { get; set; }

        [StringLength(500)]
        public string CoverImagePath { get; set; }

        [Required]
        [StringLength(500)]
        public string FolderPath { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime LastModifiedDate { get; set; }

        public bool IsActive { get; set; }

        public int? BookmarkParagraphID { get; set; }

        /// <summary>Author name, used in EPUB exports and other outputs.</summary>
        [StringLength(255)]
        public string Author { get; set; }

        /// <summary>Machine identifier from book.xml (e.g. 'autumn-meridian'). Null for projects not yet imported via BookML.</summary>
        [StringLength(100)]
        public string BookmlId { get; set; }

        /// <summary>Draft number currently loaded into the working copy.</summary>
        public int CurrentDraftNumber { get; set; }

        public virtual ICollection<Chapter> Chapters { get; set; }

        public BookProject()
        {
            CreatedDate = DateTime.Now;
            LastModifiedDate = DateTime.Now;
            IsActive = true;
            CurrentDraftNumber = 1;
            Chapters = new HashSet<Chapter>();
        }
    }
}
