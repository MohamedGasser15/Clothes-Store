using Clothes_DataAccess.Data;
using Clothes_DataAccess.Migrations;
using Clothes_Models.Models;
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

    }
}
