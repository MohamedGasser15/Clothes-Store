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
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;


        public HomeController(ILogger<HomeController> logger, ApplicationDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        public IActionResult Home()
        {
            return View();
        }

        public async Task<IActionResult> Shop()
        {
            // Retrieve all products including their Brand details
            var products = await _db.Products
                                    .Include(p => p.Brand) // Include Brand to get Brand Name
                                    .ToListAsync();

            return View(products); // Pass the list of products to the Shop view
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
                .Where(s => s.Quantity > 0) // Only include sizes with stock available
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
