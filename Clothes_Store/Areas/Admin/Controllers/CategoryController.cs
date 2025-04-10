﻿using Clothes_DataAccess.Data;
using Clothes_DataAccess.Repo.Interfaces;
using Clothes_Models.Models;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]

    public class CategoryController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;

        public CategoryController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<IActionResult> Index()
        {
            IEnumerable<Category> objList = await _unitOfWork.Categories.GetAll();
            return View(objList);
        }
        public async Task<IActionResult> Upsert(int id)
        {
            Category obj = new();
            if (id == null || id == 0)
            {
                return View(obj);
            }
            obj = await _unitOfWork.Categories.GetById(id);
            if (obj == null)
            {
                return NotFound();
            }
            return View(obj);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upsert(Category obj)
        {
            if (obj.Category_Id == 0)
            {
                await _unitOfWork.Categories.Add(obj);
            }
            else
            {
                _unitOfWork.Categories.UpdateAsync(obj);
            }
            await _unitOfWork.SaveAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            Category obj = new();
            obj = await _unitOfWork.Categories.GetById(id);
            if (obj == null)
            {
                return NotFound();
            }
                _unitOfWork.Categories.Delete(obj);
            await _unitOfWork.SaveAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
