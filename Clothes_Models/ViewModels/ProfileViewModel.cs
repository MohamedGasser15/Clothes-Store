using Clothes_Models.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class ProfileViewModel
    {
        public string Id { get; set; }
        public string UserId => Id;

        [Required]
        [Display(Name = "Name")]
        public string Name { get; set; }
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsTwoFactorEnabled { get; set; }

        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Address")]
        public string Address { get; set; }

        [Display(Name = "Postal Code")]
        public string PostalCode { get; set; }

        [Display(Name = "Country")]
        public string Country { get; set; }
        public List<SecurityActivity> RecentSecurityActivities { get; set; } = new List<SecurityActivity>();
        public List<UserDevice> ConnectedDevices { get; set; } = new List<UserDevice>();
    }

}
