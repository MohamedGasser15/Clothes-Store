using System.ComponentModel.DataAnnotations;

namespace Clothes_Store.Models.ViewModels
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }
}
