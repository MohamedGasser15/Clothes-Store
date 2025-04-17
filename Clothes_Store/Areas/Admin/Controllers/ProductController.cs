using Clothes_DataAccess.Data;
using Clothes_DataAccess.Repo;
using Clothes_DataAccess.Repo.Interfaces;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _db;

        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment, ApplicationDbContext db)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<Product> objList = await _db.Products
                .Include(p => p.Stocks)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .ToListAsync();
            return View(objList);
        }

        public async Task<IActionResult> Upsert(int id)
        {
            ProductViewModel obj = new();
            var Brands = await _unitOfWork.Brands.GetAll();
            obj.BrandList = Brands.Select(i => new SelectListItem
            {
                Text = i.Brand_Name,
                Value = i.Brand_Id.ToString()
            });
            var Categories = await _unitOfWork.Categories.GetAll();
            obj.CategoryList = Categories.Select(i => new SelectListItem
            {
                Text = i.Category_Name,
                Value = i.Category_Id.ToString()
            });

            if (id == 0) // Create new product
            {
                obj.Product = new Product();
                obj.Stocks = new List<Stock>();
                return View(obj);
            }

            // Edit existing product
            obj.Product = await _db.Products
                .Include(p => p.Stocks)
                .Include(p => p.Category)
                .Include(p => p.Brand)
                .FirstOrDefaultAsync(p => p.Product_Id == id);

            if (obj.Product == null)
            {
                return NotFound();
            }
            obj.Stocks = obj.Product.Stocks.ToList();
            return View(obj);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(ProductViewModel obj, IFormFile? file)
        {
            // Repopulate dropdowns in case of validation errors
            var Brands = await _unitOfWork.Brands.GetAll();
            obj.BrandList = Brands.Select(i => new SelectListItem
            {
                Text = i.Brand_Name,
                Value = i.Brand_Id.ToString()
            });
            var Categories = await _unitOfWork.Categories.GetAll();
            obj.CategoryList = Categories.Select(i => new SelectListItem
            {
                Text = i.Category_Name,
                Value = i.Category_Id.ToString()
            });

            // Handle image upload
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            if (file != null)
            {
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                string productPath = Path.Combine(wwwRootPath, @"img", @"products");

                // Delete old image if it exists (for updates)
                if (!string.IsNullOrEmpty(obj.Product.imgUrl))
                {
                    var oldImagePath = Path.Combine(wwwRootPath, obj.Product.imgUrl.TrimStart('\\'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }
                obj.Product.imgUrl = @"\img\products\" + fileName;
            }
            else if (obj.Product.Product_Id != 0)
            {
                // Retain existing image if no new file is uploaded
                var existingProduct = await _unitOfWork.Products.GetById(obj.Product.Product_Id);
                obj.Product.imgUrl = existingProduct?.imgUrl;
            }

            if (obj.Product.Product_Id == 0) // Create
            {
                await _unitOfWork.Products.Add(obj.Product);
                await _unitOfWork.SaveAsync(); // Save product to get Product_Id

                // Add stock entries
                if (obj.Stocks != null && obj.Stocks.Any())
                {
                    foreach (var stock in obj.Stocks)
                    {
                        stock.Product_Id = obj.Product.Product_Id;
                        _db.Stocks.Add(stock);
                    }
                }
                TempData["Success"] = "Product Added successfully";
            }
            else // Update
            {
                _unitOfWork.Products.UpdateAsync(obj.Product);

                // Remove existing stock entries
                var existingStocks = await _db.Stocks
                    .Where(s => s.Product_Id == obj.Product.Product_Id)
                    .ToListAsync();
                _db.Stocks.RemoveRange(existingStocks);

                // Add new stock entries
                if (obj.Stocks != null && obj.Stocks.Any())
                {
                    foreach (var stock in obj.Stocks)
                    {
                        stock.Product_Id = obj.Product.Product_Id;
                        _db.Stocks.Add(stock);
                    }
                }
                TempData["Success"] = $"('{obj.Product.Product_Name}') updated successfully";
            }

            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var obj = await _db.Products
                .Include(p => p.Stocks)
                .FirstOrDefaultAsync(p => p.Product_Id == id);

            if (obj == null)
            {
                TempData["Error"] = "Oops! Something went wrong. Please try again.";
                return NotFound();
            }

            // Delete associated stock entries
            if (obj.Stocks != null && obj.Stocks.Any())
            {
                _db.Stocks.RemoveRange(obj.Stocks);
            }

            // Delete the image file if it exists
            if (!string.IsNullOrEmpty(obj.imgUrl))
            {
                var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, obj.imgUrl.TrimStart('\\'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            await _unitOfWork.Products.Delete(obj);
            TempData["Success"] = "Product deleted successfully!";
            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
