using Clothes_DataAccess.Data;
using Clothes_DataAccess.Repo.Interfaces;
using Clothes_Models.Models;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]
    public class BrandController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public BrandController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Displays the list of all brands
        public async Task<IActionResult> Index()
        {
            IEnumerable<Brand> objList = await _unitOfWork.Brands.GetAll();
            return View(objList);
        }

        // Displays the form for creating or editing a brand
        public async Task<IActionResult> Upsert(int id)
        {
            Brand obj = new();
            if (id == 0)
                return View(obj);

            obj = await _unitOfWork.Brands.GetById(id);
            if (obj == null)
                return NotFound();

            return View(obj);
        }

        // Creates or updates a brand
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Brand obj)
        {
            if (obj.Brand_Id == 0)
            {
                await _unitOfWork.Brands.Add(obj);
                TempData["Success"] = "Brand Added successfully";
            }
            else
            {
                _unitOfWork.Brands.UpdateAsync(obj);
                TempData["Success"] = $"('{obj.Brand_Name}') Brand updated successfully";
            }

            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        // Deletes a brand by ID
        public async Task<IActionResult> Delete(int id)
        {
            var obj = await _unitOfWork.Brands.GetById(id);
            if (obj == null)
            {
                TempData["Error"] = "Oops! Something went wrong. Please try again.";
                return NotFound();
            }

            _unitOfWork.Brands.Delete(obj);
            TempData["Success"] = "Brand deleted successfully!";
            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}