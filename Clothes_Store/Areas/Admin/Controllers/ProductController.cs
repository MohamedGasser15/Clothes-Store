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
                .OrderByDescending(p => p.Product_Id)
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

            // Populate CategoryList with hierarchical display
            var categories = await _db.Categories.ToListAsync();
            var categoryLookup = categories.ToDictionary(c => c.Category_Id, c => c.Category_Name);
            obj.CategoryList = categories.Select(i => new SelectListItem
            {
                Text = i.ParentCategoryId == null
                    ? i.Category_Name
                    : $"-- {(categoryLookup.TryGetValue(i.ParentCategoryId.Value, out var parentName) ? parentName : "Unknown")} > {i.Category_Name}",
                Value = i.Category_Id.ToString()
            }).ToList();

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
        public async Task<IActionResult> Upsert(ProductViewModel obj, IFormFile? file, string? croppedImageData)
        {
            // Repopulate dropdowns in case of validation errors
            var Brands = await _unitOfWork.Brands.GetAll();
            obj.BrandList = Brands.Select(i => new SelectListItem
            {
                Text = i.Brand_Name,
                Value = i.Brand_Id.ToString()
            });

            var categories = await _db.Categories.ToListAsync();
            var categoryLookup = categories.ToDictionary(c => c.Category_Id, c => c.Category_Name);
            obj.CategoryList = categories.Select(i => new SelectListItem
            {
                Text = i.ParentCategoryId == null
                    ? i.Category_Name
                    : $"-- {(categoryLookup.TryGetValue(i.ParentCategoryId.Value, out var parentName) ? parentName : "Unknown")} > {i.Category_Name}",
                Value = i.Category_Id.ToString()
            }).ToList();

            var selectedCategory = await _db.Categories.FirstOrDefaultAsync(c => c.Category_Id == obj.Product.Category_Id);
            if (selectedCategory != null && selectedCategory.ParentCategoryId == null)
            {
                ModelState.AddModelError("Product.Category_Id", "Products must be assigned to a subcategory, not a parent category.");
            }

            // Handle image upload - prioritize cropped image if available
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            if (!string.IsNullOrEmpty(croppedImageData) || file != null)
            {
                string fileName;
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

                // Handle cropped image (priority)
                if (!string.IsNullOrEmpty(croppedImageData))
                {
                    // Extract base64 data
                    var base64Data = croppedImageData.Split(',')[1];
                    var bytes = Convert.FromBase64String(base64Data);

                    // Generate unique filename with appropriate extension
                    string extension = croppedImageData.StartsWith("data:image/jpeg") ? ".jpg" : ".png";
                    fileName = Guid.NewGuid().ToString() + extension;

                    // Save the file
                    await System.IO.File.WriteAllBytesAsync(Path.Combine(productPath, fileName), bytes);
                }
                else // Fallback to regular file upload
                {
                    fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
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
                obj.Product.DateAdded = DateTime.Now; // Set DateAdded to current date
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

            var legacyProducts = _db.Products
                  .Where(p => p.DateAdded == DateTime.MinValue) // Target only bad dates
                  .ToList();
            if (legacyProducts.Any())
            {
                var referenceDate = DateTime.Now.AddDays(-60); // 60 days old
                foreach (var product in legacyProducts)
                {
                    product.DateAdded = referenceDate;
                }
                await _db.SaveChangesAsync(); // Save changes to DB
            }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MakeFeatured(int id)
        {
            var obj = await _db.Products.FirstOrDefaultAsync(p => p.Product_Id == id);
            if (obj == null)
            {
                TempData["Error"] = "Oops! Something went wrong. Please try again.";
                return NotFound();
            }

            if (obj.IsFeatured)
            {
                obj.IsFeatured = false;
                TempData["Success"] = "Product has been removed from featured items!";
            }
            else
            {
                obj.IsFeatured = true;
                TempData["Success"] = "Product has been added to featured items!";
            }

            await _unitOfWork.Products.UpdateAsync(obj);
            await _unitOfWork.SaveAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
