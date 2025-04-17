using Clothes_Models.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class ProductViewModel
    {
        public Product Product { get; set; }
        public IEnumerable<SelectListItem> BrandList { get; set; }
        public IEnumerable<SelectListItem> CategoryList { get; set; }
        public List<Stock> Stocks { get; set; } = new List<Stock>();
    }
}
