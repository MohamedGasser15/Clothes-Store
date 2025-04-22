using System.Diagnostics;
using System.Linq.Expressions;
using System.Security.Claims;
using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Store.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SendGrid.Helpers.Mail;

namespace Clothes_Store.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;


        public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }
       
        public IActionResult ShopByCategory(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                return NotFound();
            }

            // Map the URL-friendly category to the database category name
            var categoryName = category switch
            {
                "mens-fashion" => "Men's Fashion",
                "womens-fashion" => "Women's Fashion",
                "kids-fashion" => "Kids Fashion",
                "footwear" => "Footwear",
                "accessories" => "Accessories",
                "sportswear" => "Sportswear",
                "luxury-collection" => "Luxury Collection",
                "summer-essentials" => "Seasonal Collections",
                _ => null
            };

            if (categoryName == null)
            {
                return NotFound();
            }

            // Get the parent category and its subcategories
            var parentCategory = _db.Categories.FirstOrDefault(c => c.Category_Name == categoryName && c.ParentCategoryId == null);
            if (parentCategory == null)
            {
                return NotFound();
            }

            // Get all subcategory IDs (including the parent category)
            var categoryIds = _db.Categories
                .Where(c => c.ParentCategoryId == parentCategory.Category_Id || c.Category_Id == parentCategory.Category_Id)
                .Select(c => c.Category_Id)
                .ToList();

            // Get products for the parent category and its subcategories
            var products = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Stocks)
                .Where(p => categoryIds.Contains(p.Category_Id))
                .ToList();

            if (products == null || !products.Any())
            {
                return View(products); // Return the view even if no products, to show "No Products Found"
            }

            ViewBag.Category = categoryName;
            return View(products);
        }
        public IActionResult Home()
        {
            var productsQuery = _db.Products
                                  .Include(p => p.Brand)
                                  .Include(p => p.Category)
                                  .OrderByDescending(p => p.Product_Id);
            var products = productsQuery
                .Take(8)
                .Select(p => new
                {
                    p.Product_Id,
                    p.Product_Name,
                    p.imgUrl,
                    BrandName = p.Brand.Brand_Name,
                    p.IsFeatured,
                    p.Product_Rating,
                    p.Product_Price,
                    AvailableSizes = p.Stocks
                    .Where(s => s.Quantity > 0)
                    .Select(s => s.Size)
                    .Distinct()
                    .OrderBy(s => s)
                    .ToList()
                })
                .ToList();
            if (products == null)
            {
                return NotFound();
            }

            return View(products);
        }

        public async Task<IActionResult> Shop(int page = 1, int pageSize = 8)
        {
            var productsQuery = _db.Products
                                   .Include(p => p.Brand)
                                   .OrderBy(p => p.Product_Id);

            var totalProducts = await productsQuery.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
            page = Math.Max(1, page);
            page = Math.Min(page, totalPages);

            var products = await productsQuery
                                 .Skip((page - 1) * pageSize)
                                 .Take(pageSize)
                                 .Select(p => new
                                 {
                                     p.Product_Id,
                                     p.Product_Name,
                                     p.imgUrl,
                                     BrandName = p.Brand.Brand_Name,
                                     p.IsFeatured,
                                     p.Product_Rating,
                                     p.Product_Price,
                                     AvailableSizes = p.Stocks
                                         .Where(s => s.Quantity > 0)
                                         .Select(s => s.Size)
                                         .Distinct()
                                         .OrderBy(s => s)
                                         .ToList()
                                 })
                                 .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalProducts = totalProducts;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    products,
                    currentPage = page,
                    totalPages,
                    pageSize
                });
            }

            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _db.Products
                .Include(p => p.Category) // Include category details
                .Include(p => p.Brand) // Include brand details
                .Include(p => p.Stocks) // Include stock details to get available sizes
                .FirstOrDefaultAsync(p => p.Product_Id == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> GetProductSizes(int productId)
        {
            var product = await _db.Products
                .Include(p => p.Stocks)
                .FirstOrDefaultAsync(p => p.Product_Id == productId);

            if (product == null)
            {
                return Json(new { success = false, message = "Product not found" });
            }

            var sizes = product.Stocks?
                .Where(s => s.Quantity > 0)
                .Select(s => s.Size)
                .ToList() ?? new List<string>();

            return Json(new { success = true, sizes });
        }

        public ActionResult Search(string searchTerm)
        {
            var viewModel = new SearchViewModel
            {
                SearchTerm = searchTerm
            };

            if (!string.IsNullOrEmpty(searchTerm))
            {
                viewModel.Results = _db.Products.Include(p => p.Brand)
                    .Where(p => p.Product_Name.Contains(searchTerm) ||
                                p.Product_Description.Contains(searchTerm))
                    .ToList();
            }
            else
            {
                viewModel.Results = new List<Product>();
            }

            return View(viewModel);
        }

        public IActionResult About()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
