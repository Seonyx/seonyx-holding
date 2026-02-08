using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("ContactSubmissions")]
    public class ContactSubmission
    {
        [Key]
        public int SubmissionId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(320)]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }

        [StringLength(45)]
        public string IpAddress { get; set; }

        [StringLength(500)]
        public string UserAgent { get; set; }

        public bool IsRead { get; set; }

        public bool IsSpam { get; set; }

        public DateTime SubmittedDate { get; set; }

        public ContactSubmission()
        {
            SubmittedDate = DateTime.Now;
        }
    }
}
