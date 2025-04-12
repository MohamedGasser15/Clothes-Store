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

        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            IEnumerable<Product> objList = await _unitOfWork.Products.GetAll();
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
            if (id == null || id == 0)
            {
                return View(obj);
            }
            obj.Product = await _unitOfWork.Products.GetById(id);
            if (obj == null)
            {
                return NotFound();
            }
            return View(obj);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(ProductViewModel obj, IFormFile? file)
        {
            string wwwRootPath = _webHostEnvironment.WebRootPath;
            if (file != null)
            {
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                string productPath = Path.Combine(wwwRootPath, @"img" , @"products");

                using (var fileStream = new FileStream(Path.Combine(productPath, fileName), FileMode.Create))
                {
                    file.CopyTo(fileStream);
                }
                obj.Product.imgUrl = @"\img\products\" + fileName;
            }
            if (obj.Product.Product_Id == 0)
            {
                await _unitOfWork.Products.Add(obj.Product);
            }
            else
            {
                _unitOfWork.Products.UpdateAsync(obj.Product);
            }
            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            Product obj = new();
            obj = await _unitOfWork.Products.GetById(id);
            if (obj == null)
            {
                return NotFound();
            }
            _unitOfWork.Products.Delete(obj);
            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
