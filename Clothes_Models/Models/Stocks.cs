using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class Stock
    {
        [Key]
        public int Stock_Id { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }

        [ForeignKey("Product")]
        public int Product_Id { get; set; }
        public Product Product { get; set; }
    }
}
