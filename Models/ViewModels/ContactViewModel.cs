using System.ComponentModel.DataAnnotations;

namespace Seonyx.Web.Models.ViewModels
{
    public class ContactViewModel
    {
        [Required(ErrorMessage = "Please enter your name")]
        [StringLength(200)]
        [Display(Name = "Your Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Please enter your email address")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(320)]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [StringLength(500)]
        [Display(Name = "Subject")]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Please enter your message")]
        [Display(Name = "Message")]
        public string Message { get; set; }

        // Honeypot field - should remain empty
        public string Website { get; set; }
    }
}
