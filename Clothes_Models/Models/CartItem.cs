using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class CartItem
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }
        public string UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
        public int Quantity { get; set; }
        public string Size { get; set; }
        [NotMapped]
        public decimal Total => Quantity * (Product?.Product_Price ?? 0);
    }
}
