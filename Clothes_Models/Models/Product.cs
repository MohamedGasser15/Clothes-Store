using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class Product
    {
        [Key]
        public int Product_Id { get; set; }
        [Required]
        public string Product_Name { get; set; }
        public string Product_Description { get; set; }
        public decimal Product_Price { get; set; }
        public string imgUrl { get; set; }
        public int Product_Rating { get; set; }
        public string Product_Size { get; set; }
        public string Product_Color { get; set; }

        [ForeignKey("Category")]
        public int Category_Id { get; set; }
        public Category Category { get; set; }

        [ForeignKey("Brand")]
        public int brand_Id { get; set; }
        public Brand Brand { get; set; }
    }
}
