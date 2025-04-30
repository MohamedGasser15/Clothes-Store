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
    public class CategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserManager<ApplicationUser> _userManager;

        public CategoryController(IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        // Displays the list of all categories
        public async Task<IActionResult> Index()
        {
            IEnumerable<Category> objList = await _unitOfWork.Categories.GetAll();
            return View(objList);
        }

        // Displays the form for creating or editing a category
        public async Task<IActionResult> Upsert(int id)
        {
            Category obj = new();
            if (id == 0)
                return View(obj);

            obj = await _unitOfWork.Categories.GetById(id);
            if (obj == null)
                return NotFound();

            return View(obj);
        }

        // Creates or updates a category
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Category obj)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            if (obj.Category_Id == 0)
            {
                await _unitOfWork.Categories.Add(obj);
                await _unitOfWork.Categories.AdminActivityAsync(
                    userId: user.Id,
                    activityType: "AddCategory",
                       description: $"Add Category(Id: {obj.Category_Id})",
                    ipAddress: HttpContext.Connection.RemoteIpAddress.ToString()
                );
                TempData["Success"] = "Category Added successfully";
            }
            else
            {
                _unitOfWork.Categories.UpdateAsync(obj);
                await _unitOfWork.Categories.AdminActivityAsync(
                    userId: user.Id,
                    activityType: "UpdateCategory",
                description: $"Update Category (Id: {obj.Category_Id})",

                    ipAddress: HttpContext.Connection.RemoteIpAddress.ToString()
                );
                TempData["Success"] = $"('{obj.Category_Name}') updated successfully";
            }

            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        // Deletes a category by ID
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            var obj = await _unitOfWork.Categories.GetById(id);
            if (obj == null)
            {
                TempData["Error"] = "Oops! Something went wrong. Please try again.";
                return NotFound();
            }
            else
            {
            _unitOfWork.Categories.Delete(obj);
                await _unitOfWork.Categories.AdminActivityAsync(
                userId: user.Id,
                activityType: "DeleteCategory",
                description: $"Delete Category (Id: {obj.Category_Id})",
                ipAddress: HttpContext.Connection.RemoteIpAddress.ToString()
            );
            TempData["Success"] = "Category deleted successfully!";
            await _unitOfWork.SaveAsync();
            }

            return RedirectToAction(nameof(Index));
        }
    }
}