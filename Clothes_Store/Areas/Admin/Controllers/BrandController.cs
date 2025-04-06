using Clothes_DataAccess.Data;
using Clothes_DataAccess.Migrations;
using Clothes_DataAccess.Repo.Interfaces;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class BrandController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;


        public BrandController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<IActionResult> Index()
        {
            IEnumerable<Brand> objList = await _unitOfWork.Brands.GetAll();
            return View(objList);
        }
        public async Task<IActionResult> Upsert(int id)
        {
            Brand obj = new();
            if (id == null || id == 0)
            {
                return View(obj);
            }
            obj = await _unitOfWork.Brands.GetById(id);
            if (obj == null)
            {
                return NotFound();
            }
            return View(obj);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Brand obj)
        {

            if (obj.Brand_Id == 0)
            {
                await _unitOfWork.Brands.Add(obj);
            }
            else
            {
                _unitOfWork.Brands.UpdateAsync(obj);
            }
            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            Brand obj = new();
            obj = await _unitOfWork.Brands.GetById(id);

            if (obj == null)
            {
                return NotFound();
            }
            else
            {
                _unitOfWork.Brands.Delete(obj);
            }
            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
