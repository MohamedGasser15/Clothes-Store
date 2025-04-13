using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;
        public OrderController(ApplicationDbContext db)
        {
            _db = db;
        }
        public IActionResult Index()
        {
            List<OrderHeader> objOrderHeaders = _db.OrderHeaders
            .Include(a => a.ApplicationUser)
            .ToList();

            return View(objOrderHeaders);
        }
        public IActionResult Details(int id)
        {
            OrderVM orderVM = new()
            {
                OrderHeader = _db.OrderHeaders
                    .Include(u => u.ApplicationUser)
                    .FirstOrDefault(u => u.Id == id),
                OrderDetails = _db.OrderDetails
                    .Where(u => u.OrderHeaderId == id)
                    .Include(u => u.Product)
                    .ToList()
            };

            return View(orderVM);
        }

    }
}
