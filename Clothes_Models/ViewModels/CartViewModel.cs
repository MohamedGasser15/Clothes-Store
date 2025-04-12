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
     public OrderHeader OrderHeader { get; set; }

     // Calculated properties
     public double Subtotal => (double)(Items?.Sum(i => i.Product.Product_Price * i.Quantity) ?? 0);
     public double ShippingFee => Subtotal > 100 ? 0 : 10.00; // Free shipping over $100
     public double Tax => Subtotal * 0.08; // 8% tax rate
     public double Total => Subtotal + ShippingFee + Tax;
     public int TotalItems => Items?.Sum(i => i.Quantity) ?? 0;
 }
}
