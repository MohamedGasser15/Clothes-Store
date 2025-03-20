using System.Diagnostics;
using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Store.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clothes_Store.Controllers
{
    [Area("Customer")]

    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _db;


        public HomeController(ILogger<HomeController> logger , ApplicationDbContext db)
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

        public IActionResult Blog()
        {
            return View();
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
