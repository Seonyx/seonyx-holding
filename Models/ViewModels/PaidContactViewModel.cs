using System.ComponentModel.DataAnnotations;

namespace Seonyx.Web.Models.ViewModels
{
    public class PaidContactViewModel
    {
        [Required(ErrorMessage = "Please enter your name.")]
        [MaxLength(200)]
        [Display(Name = "Full Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [MaxLength(320)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [MaxLength(200)]
        [Display(Name = "Company / Organisation")]
        public string Company { get; set; }

        [Required(ErrorMessage = "Please enter a subject.")]
        [MaxLength(500)]
        [Display(Name = "Subject")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Please enter your message.")]
        [MinLength(20, ErrorMessage = "Your message must be at least 20 characters.")]
        [Display(Name = "Message")]
        public string Message { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Terms and Conditions and Privacy Policy.")]
        [Display(Name = "Terms & Conditions")]
        public bool AgreeTerms { get; set; }

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must acknowledge the non-refundable nature of the contact fee.")]
        [Display(Name = "Cooling-off Waiver")]
        public bool WaiveCoolingOff { get; set; }

        // Honeypot -- never shown to the user; bots fill it in
        public string Website { get; set; }
    }

    public class ContactSuccessViewModel
    {
        public string Email { get; set; }
        public string CheckoutSessionId { get; set; }
        public string PaymentIntentId { get; set; }
    }
}
