using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string Name { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; } = "Egypt";
        public string? StreetAddress { get; set; }
        public string? StreetAddress2 { get; set; }
        public string? SelectedAddress { get; set; }

        [NotMapped]
        public string? NewStreetAddress { get; set; }

        [NotMapped]
        public string? NewStreetAddress2 { get; set; }
        public string? PreferredLanguage { get; set; } = "en";
        public string? Currency { get; set; } = "USD";
        public string? PaymentMehtod { get; set; } = "Cash";
        public string? PreferredCarriers { get; set; } = "Bosta";



        [NotMapped]
        public string RoleId { get; set; }
        [NotMapped]
        public string Role { get; set; }
        [NotMapped]
        public IEnumerable<SelectListItem> RoleList { get; set; }
        public virtual ICollection<CartItem> CartItems { get; set; }
    }
}
