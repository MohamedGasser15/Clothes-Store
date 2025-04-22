using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.Models
{
    public class Category
    {
        [Key]
        public int Category_Id { get; set; }

        [Required]
        public string Category_Name { get; set; }

        // Add ParentCategoryId for self-referencing relationship
        public int? ParentCategoryId { get; set; }

        // Navigation property for the parent category
        public Category ParentCategory { get; set; }

        // Navigation property for subcategories
        public ICollection<Category> Subcategories { get; set; }
    }
}