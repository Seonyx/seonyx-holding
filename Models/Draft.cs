using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("Drafts")]
    public class Draft
    {
        [Key]
        public int DraftID { get; set; }

        public int BookProjectID { get; set; }

        public int DraftNumber { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; }          // in-progress | snapshot | abandoned

        public DateTime CreatedDate { get; set; }

        public int BasedOn { get; set; }             // parent draft number; 0 = initial generation

        [Required]
        [StringLength(10)]
        public string AuthorType { get; set; }       // human | ai

        [Required]
        [StringLength(200)]
        public string Author { get; set; }

        [StringLength(200)]
        public string Label { get; set; }

        public DateTime? ExportDate { get; set; }

        public string DraftNote { get; set; }

        [ForeignKey("BookProjectID")]
        public virtual BookProject BookProject { get; set; }

        public Draft()
        {
            CreatedDate = DateTime.Now;
            Status = "in-progress";
        }
    }
}
