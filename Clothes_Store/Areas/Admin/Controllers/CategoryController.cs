using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]

    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _db;

        public CategoryController(ApplicationDbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            List<Category> objList = _db.Categories.ToList();
            return View(objList);
        }
    }
}
