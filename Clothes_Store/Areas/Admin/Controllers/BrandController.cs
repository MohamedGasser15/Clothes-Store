using Clothes_DataAccess.Data;
using Clothes_DataAccess.Repo.Interfaces;
using Clothes_Models.Models;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]
    public class BrandController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;

        public BrandController(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
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
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (obj.Brand_Id == 0)
            {
                await _unitOfWork.Brands.Add(obj);
                await _unitOfWork.Brands.AdminActivityAsync(
                     userId: user.Id,
                     activityType: "AddBrand",
                     description: $"Add Brand (Id: {obj.Brand_Id})",
                     ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
                    );
                TempData["Success"] = "Brand Added successfully";
            }
            else
            {
                _unitOfWork.Brands.UpdateAsync(obj);
                await _unitOfWork.Brands.AdminActivityAsync(
                     userId: user.Id,
                     activityType: "UpdateBrand",
                     description: $"Update Brand (Id: {obj.Brand_Id})",
                     ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
                    );
                TempData["Success"] = $"'{obj.Brand_Name}' Brand updated successfully";
            }

            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        // Deletes a brand by ID
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            var obj = await _unitOfWork.Brands.GetById(id);
            if (obj == null)
            {
                TempData["Error"] = "Oops! Something went wrong. Please try again.";
                return NotFound();
            }
            else
            {
                _unitOfWork.Brands.Delete(obj);
            await _unitOfWork.Brands.AdminActivityAsync(
                     userId: user.Id,
                     activityType: "RemoveBrand",
                     description: $"Remove Brand (Id: {obj.Brand_Id})",
                     ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString()
                    );
            TempData["Success"] = "Brand deleted successfully!";
            await _unitOfWork.SaveAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}