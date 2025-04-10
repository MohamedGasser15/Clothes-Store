using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class TwoFactorSetupViewModel
    {
        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
        [RegularExpression(@"^\d+$", ErrorMessage = "Only numbers are allowed")]
        public string Code { get; set; }

        public string Email { get; set; }
    }
}
