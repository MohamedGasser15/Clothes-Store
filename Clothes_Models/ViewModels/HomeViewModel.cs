using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class HomeViewModel
    {
        public int Product_Id { get; set; }
        public string Product_Name { get; set; }
        public string imgUrl { get; set; }
        public string BrandName { get; set; }
        public bool IsFeatured { get; set; }
        public DateTime DateAdded { get; set; }
        public int Product_Rating { get; set; }
        public decimal Product_Price { get; set; }
        public List<string> AvailableSizes { get; set; }
        public List<HomeViewModel> FeaturedProducts { get; set; }
        public List<HomeViewModel> NewArrivals { get; set; }
        public List<HomeViewModel> FilteredProducts { get; set; }
    }
}
