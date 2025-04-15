using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class PaymentMethod
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        // Stripe-related fields
        public string? StripePaymentMethodId { get; set; }
        public string? StripeCustomerId { get; set; }

        // Card details (safe to store - PCI compliant)
        public string? CardBrand { get; set; } // visa, mastercard, amex, etc.
        public string? Last4 { get; set; }     // Last 4 digits only
        public int ExpMonth { get; set; }
        public int ExpYear { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        // Billing address
        public string? BillingName { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; } = "US"; // Default to US
        public string? PhoneNumber { get; set; }// Default to US

        // Navigation property
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}
