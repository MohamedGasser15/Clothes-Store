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

        // Displays home page with featured and new arrival products
        public async Task<IActionResult> Home(int? categoryId = null)
        {
            ViewBag.CartCount = GetCartCount();

            var productsQuery = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .OrderBy(p => p.Product_Id);

            if (categoryId.HasValue && categoryId > 0)
            {
                var subCategoryIds = await _db.Categories
                    .Where(c => c.ParentCategoryId == categoryId.Value)
                    .Select(c => c.Category_Id)
                    .ToListAsync();

                productsQuery = (IOrderedQueryable<Product>)productsQuery
                    .Where(p => subCategoryIds.Any() ? subCategoryIds.Contains(p.Category_Id) : p.Category_Id == categoryId.Value);
            }
            else
            {
                productsQuery = (IOrderedQueryable<Product>)productsQuery
                    .Where(p => p.IsFeatured);
            }

            var products = await productsQuery
                .Take(8)
                .Select(p => new HomeViewModel
                {
                    Product_Id = p.Product_Id,
                    Product_Name = p.Product_Name,
                    imgUrl = p.imgUrl,
                    BrandName = p.Brand != null ? p.Brand.Brand_Name : "Unknown",
                    IsFeatured = p.IsFeatured,
                    DateAdded = p.DateAdded,
                    Product_Rating = p.Product_Rating,
                    Product_Price = p.Product_Price,
                    AvailableSizes = p.Stocks
                        .Where(s => s.Quantity > 0)
                        .Select(s => s.Size)
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList()
                })
                .ToListAsync();

            var newArrivals = await _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .OrderByDescending(p => p.Product_Id)
                .Take(8)
                .Select(p => new HomeViewModel
                {
                    Product_Id = p.Product_Id,
                    Product_Name = p.Product_Name,
                    imgUrl = p.imgUrl,
                    BrandName = p.Brand != null ? p.Brand.Brand_Name : "Unknown",
                    IsFeatured = p.IsFeatured,
                    DateAdded = p.DateAdded,
                    Product_Rating = p.Product_Rating,
                    Product_Price = p.Product_Price,
                    AvailableSizes = p.Stocks
                        .Where(s => s.Quantity > 0)
                        .Select(s => s.Size)
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList()
                })
                .ToListAsync();

            var specificCategories = new[] { "Bags", "Jackets", "Hats", "Shoes", "Pantalons", "Dresses", "Shorts", "Hoodie" };
            var categories = await _db.Categories
                .Where(c => specificCategories.Contains(c.Category_Name) && c.ParentCategoryId != null)
                .Select(c => new
                {
                    Name = c.Category_Name,
                    ImgUrl = $"/img/categories/{c.Category_Name.ToLower()}.jpg",
                    Link = Url.Action("ShopByChildCategory", "Home", new { area = "Customer", category = c.Category_Name.ToLower() })
                })
                .ToListAsync();

            var specificBrands = new[] { "Nike", "Adidas", "Zara", "H&M", "Puma", "Levi's", "Lacoste", "Local Brands" };
            var brands = specificBrands
                .Select(b => new
                {
                    Name = b,
                    ImgUrl = $"/img/brands/{b.ToLower().Replace("'", "").Replace("&", "")}.jpg",
                    Link = Url.Action("ShopByBrand", "Home", new { area = "Customer", brand = b.ToLower().Replace("'", "").Replace("&", "") })
                })
                .ToList();

            ViewBag.CurrentCategoryId = categoryId;
            ViewBag.Categories = categories;
            ViewBag.Brands = brands;

            var viewModel = new HomeViewModel
            {
                FeaturedProducts = products,
                NewArrivals = newArrivals
            };

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { products, currentCategoryId = categoryId });

            return View(viewModel);
        }

        // Displays paginated shop page with optional category filter
        public async Task<IActionResult> Shop(int? categoryId = null, int page = 1, int pageSize = 8)
        {
            ViewBag.CartCount = GetCartCount();

            page = Math.Max(1, page);
            pageSize = Math.Max(1, Math.Min(pageSize, 100));

            var productsQuery = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .OrderBy(p => p.Product_Id);

            if (categoryId.HasValue && categoryId > 0)
            {
                var subCategoryIds = await _db.Categories
                    .Where(c => c.ParentCategoryId == categoryId.Value)
                    .Select(c => c.Category_Id)
                    .ToListAsync();

                productsQuery = (IOrderedQueryable<Product>)productsQuery
                    .Where(p => subCategoryIds.Any() ? subCategoryIds.Contains(p.Category_Id) : p.Category_Id == categoryId.Value);
            }

            var totalProducts = await productsQuery.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalProducts / pageSize);
            page = Math.Min(page, Math.Max(1, totalPages));

            var products = await productsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    ProductId = p.Product_Id,
                    ProductName = p.Product_Name,
                    ImgUrl = p.imgUrl,
                    BrandName = p.Brand.Brand_Name,
                    CategoryName = p.Category.Category_Name,
                    IsFeatured = p.IsFeatured,
                    ProductRating = p.Product_Rating,
                    ProductPrice = p.Product_Price,
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
            ViewBag.CurrentCategoryId = categoryId;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { products, currentPage = page, totalPages, pageSize, currentCategoryId = categoryId });

            return View(products);
        }

        // Displays detailed view of a single product
        public async Task<IActionResult> Details(int id)
        {
            ViewBag.CartCount = GetCartCount();

            var product = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .Include(p => p.Stocks)
                .FirstOrDefaultAsync(p => p.Product_Id == id);

            return product == null ? NotFound() : View(product);
        }

        [HttpGet]
        // Fetches product details by ID for JSON response
        public IActionResult GetProductDetails(int productId)
        {
            ViewBag.CartCount = GetCartCount();

            var product = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Stocks)
                .Where(p => p.Product_Id == productId)
                .Select(p => new
                {
                    productId = p.Product_Id,
                    productName = p.Product_Name,
                    imgUrl = p.imgUrl,
                    productRating = p.Product_Rating,
                    productPrice = p.Product_Price,
                    description = p.Product_Description,
                    color = p.Product_Color,
                    brandName = p.Brand.Brand_Name,
                    categoryName = p.Category.Category_Name,
                    availableSizes = p.Stocks
                        .Where(s => s.Quantity > 0)
                        .Select(s => s.Size)
                        .Distinct()
                        .OrderBy(s => s)
                        .ToList()
                })
                .FirstOrDefault();

            return product == null
                ? Json(new { success = false, message = "Product not found" })
                : Json(new { success = true, product });
        }

        // Displays products by main category
        public IActionResult ShopByCategory(string category)
        {
            ViewBag.CartCount = GetCartCount();

            if (string.IsNullOrEmpty(category))
                return NotFound();

            // Map URL-friendly category to database category name
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
                return NotFound();

            var parentCategory = _db.Categories.FirstOrDefault(c => c.Category_Name == categoryName && c.ParentCategoryId == null);
            if (parentCategory == null)
                return NotFound();

            var categoryIds = _db.Categories
                .Where(c => c.ParentCategoryId == parentCategory.Category_Id || c.Category_Id == parentCategory.Category_Id)
                .Select(c => c.Category_Id)
                .ToList();

            var products = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Stocks)
                .Where(p => categoryIds.Contains(p.Category_Id))
                .ToList();

            ViewBag.Category = categoryName;
            return View(products ?? new List<Product>());
        }

        // Displays products by child category
        public IActionResult ShopByChildCategory(string category)
        {
            ViewBag.CartCount = GetCartCount();

            if (string.IsNullOrEmpty(category))
                return NotFound();

            var categoryName = category.ToLower();

            if (categoryName == "shorts" || categoryName == "hoodie")
            {
                var parentCategoryIds = _db.Categories
                    .Where(c => c.Category_Name == "Men's Fashion" || c.Category_Name == "Women's Fashion" || c.Category_Name == "Unisex")
                    .Select(c => c.Category_Id)
                    .ToList();

                var childCategoryIds = _db.Categories
                    .Where(c => c.Category_Name.ToLower() == categoryName && c.ParentCategoryId.HasValue && parentCategoryIds.Contains(c.ParentCategoryId.Value))
                    .Select(c => c.Category_Id)
                    .ToList();

                if (!childCategoryIds.Any())
                    return NotFound();

                var product = _db.Products
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.Stocks)
                    .Where(p => childCategoryIds.Contains(p.Category_Id))
                    .ToList();

                ViewBag.Category = categoryName == "shorts" ? "Shorts" : "Hoodie";
                return View("ShopByCategory", product ?? new List<Product>());
            }

            var childCategory = _db.Categories
                .FirstOrDefault(c => c.Category_Name.ToLower() == categoryName && c.ParentCategoryId != null);

            if (childCategory == null)
                return NotFound();

            var products = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Stocks)
                .Where(p => p.Category_Id == childCategory.Category_Id)
                .ToList();

            ViewBag.Category = childCategory.Category_Name;
            return View("ShopByCategory", products ?? new List<Product>());
        }

        // Displays products by brand
        public IActionResult ShopByBrand(string brand)
        {
            ViewBag.CartCount = GetCartCount();

            if (string.IsNullOrEmpty(brand))
                return NotFound();

            var brandLower = brand.ToLower();
            bool isLocalBrands = brandLower == "local";

            var brandName = isLocalBrands ? null : brandLower switch
            {
                "nike" => "Nike",
                "adidas" => "Adidas",
                "puma" => "Puma",
                "zara" => "Zara",
                "hm" => "H&M",
                "levis" => "Levi's",
                "nightbird" => "NightBird",
                _ => null
            };

            if (!isLocalBrands && _db.Brands.FirstOrDefault(b => b.Brand_Name == brandName) == null)
                return NotFound();

            var products = _db.Products
                .Include(p => p.Brand)
                .Include(p => p.Category)
                .Include(p => p.Stocks)
                .Where(p => isLocalBrands
                    ? p.Brand != null && !new[] { "Nike", "Adidas", "Puma", "Zara", "H&M", "Levi's", "NightBird" }.Contains(p.Brand.Brand_Name)
                    : p.Brand.Brand_Name == brandName)
                .ToList();

            ViewBag.Brand = isLocalBrands ? "Local Brands" : brandName;
            return View("ShopByBrand", products ?? new List<Product>());
        }

        [HttpGet]
        // Fetches available sizes for a product
        public async Task<IActionResult> GetProductSizes(int productId)
        {
            ViewBag.CartCount = GetCartCount();

            var product = await _db.Products
                .Include(p => p.Stocks)
                .FirstOrDefaultAsync(p => p.Product_Id == productId);

            if (product == null)
                return Json(new { success = false, message = "Product not found" });

            var sizes = product.Stocks?
                .Where(s => s.Quantity > 0)
                .Select(s => s.Size)
                .ToList() ?? new List<string>();

            return Json(new { success = true, sizes });
        }

        // Handles product search by name or description
        public IActionResult Search(string searchTerm)
        {
            ViewBag.CartCount = GetCartCount();

            var viewModel = new SearchViewModel { SearchTerm = searchTerm };

            if (!string.IsNullOrEmpty(searchTerm))
            {
                viewModel.Results = _db.Products
                    .Include(p => p.Brand)
                    .Where(p => p.Product_Name.Contains(searchTerm) || p.Product_Description.Contains(searchTerm))
                    .ToList();
            }
            else
            {
                viewModel.Results = new List<Product>();
            }

            return View(viewModel);
        }

        // Displays the About page
        public IActionResult About()
        {
            ViewBag.CartCount = GetCartCount();
            return View();
        }

        // Displays the Contact page
        public IActionResult Contact()
        {
            ViewBag.CartCount = GetCartCount();
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        // Handles error display
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Retrieves cart item count for authenticated users
        private int GetCartCount()
        {
            if (!User.Identity.IsAuthenticated)
                return 0;

            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value;
            return _db.CartItems.Count(c => c.UserId == userId);
        }
    }
}