using Clothes_Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class SearchViewModel
    {
        public string SearchTerm { get; set; }
        public List<Product> Results { get; set; }
    }
}
