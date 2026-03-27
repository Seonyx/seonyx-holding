using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Seonyx.Web.Models
{
    [Table("PaidContactSubmissions")]
    public class PaidContactSubmission
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(36)]
        public string ReferenceId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; }

        [Required, MaxLength(320)]
        public string Email { get; set; }

        [MaxLength(200)]
        public string Company { get; set; }

        [Required, MaxLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }

        [MaxLength(45)]
        public string IpAddress { get; set; }

        [MaxLength(500)]
        public string UserAgent { get; set; }

        [MaxLength(200)]
        public string StripeCheckoutSessionId { get; set; }

        [MaxLength(200)]
        public string StripePaymentIntentId { get; set; }

        public int? AmountPaid { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; }

        public DateTime SubmittedDate { get; set; }

        public DateTime? ProcessedDate { get; set; }
    }
}
