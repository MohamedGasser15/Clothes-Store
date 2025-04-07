using Clothes_Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class CartViewModel
    {
        public IEnumerable<CartItem> Items { get; set; }

        // Calculated properties
        public decimal Subtotal => Items?.Sum(i => i.Product.Product_Price * i.Quantity) ?? 0;

        public decimal ShippingFee => Subtotal > 100 ? 0 : 10.00m; // Free shipping over $100

        public decimal Tax => Subtotal * 0.01m; // 8% tax rate

        public decimal Total => Subtotal + ShippingFee + Tax;

        public int TotalItems => Items?.Sum(i => i.Quantity) ?? 0;
    }
}
