using Clothes_DataAccess.Data;
using Clothes_DataAccess.Migrations;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class BrandController : Controller
    {
        private readonly ApplicationDbContext _db;

        public BrandController(ApplicationDbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            List<Brand> objList = _db.Brands.ToList();
            return View(objList);
        }
        public IActionResult Upsert(int? id)
        {
            Brand obj = new();
            if (id == null || id == 0)
            {
                return View(obj);
            }
            obj = _db.Brands.FirstOrDefault(i => i.Brand_Id == id);
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
                    await _db.Brands.AddAsync(obj);
                }
                else
                {
                    _db.Brands.Update(obj);
                }
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
        }

        public IActionResult Delete(int id)
        {
            Brand obj = new();
            obj = _db.Brands.FirstOrDefault(c => c.Brand_Id == id);

            if (obj == null)
            {
                return NotFound();
            }
            else
            {
                _db.Brands.Remove(obj);
            }
            _db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}
