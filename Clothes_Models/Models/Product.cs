using System;
using System.Collections;
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

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Product name cannot exceed 100 characters")]
        public string Product_Name { get; set; }
        public string Product_Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Product_Price { get; set; }
        [Required(ErrorMessage = "Product image is required")]
        public string imgUrl { get; set; }
        public int Product_Rating { get; set; }

        [Required(ErrorMessage = "Color is required")]
        public string Product_Color { get; set; }

        public bool IsFeatured { get; set; }

        [Display(Name = "Date Added")]
        public DateTime DateAdded { get; set; } = DateTime.Now;

        [Display(Name = "New Arrival Duration (Days)")]
        public int? NewArrivalDurationDays { get; set; }

        [NotMapped]
        public bool IsNewArrival
        {
            get
            {
                if (NewArrivalDurationDays.HasValue)
                {
                    return DateAdded.AddDays(NewArrivalDurationDays.Value) >= DateTime.Now;
                }
                return DateAdded.AddDays(30) >= DateTime.Now;
            }
        }

        [ForeignKey("Category")]
        [Required(ErrorMessage = "Category is required")]
        public int Category_Id { get; set; }
        public Category Category { get; set; }

        [ForeignKey("Brand")]
        [Required(ErrorMessage = "Brand is required")]
        public int brand_Id { get; set; }
        public Brand Brand { get; set; }

        public ICollection<Stock> Stocks { get; set; }
        public ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
