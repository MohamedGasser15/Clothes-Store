using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class UserProfile
    {
        [Key]
        public string Id { get; set; } // نفس ID بتاع ApplicationUser

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Phone]
        public string PhoneNumber { get; set; }

        public string Address { get; set; }

        public string CurrentCity { get; set; }

        public string PostalCode { get; set; }

        public string Gender { get; set; } // Male - Female - Other

        [ForeignKey("Id")]
        public virtual ApplicationUser User { get; set; }
    }

}
