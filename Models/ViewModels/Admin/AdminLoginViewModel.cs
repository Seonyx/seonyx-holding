using System.ComponentModel.DataAnnotations;

namespace Seonyx.Web.Models.ViewModels.Admin
{
    public class AdminLoginViewModel
    {
        [Required(ErrorMessage = "Please enter the admin password")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }
    }
}
