using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class VerifyEmailViewModel
    {
        public string UserId { get; set; }

        [Required]
        [Display(Name = "Verification Code")]
        public string Code { get; set; }
    }
}
