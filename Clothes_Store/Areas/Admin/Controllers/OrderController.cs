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
        [HttpPost]
        [Authorize(Roles = SD.Admin)]
        public IActionResult UpdateCustomerInfo(int orderId, string name, string phoneNumber)
        {
            var orderFromDb = _db.OrderHeaders.Find(orderId);
            if (orderFromDb == null)
            {
                return NotFound();
            }

            orderFromDb.Name = name;
            orderFromDb.PhoneNumber = phoneNumber;
            _db.SaveChanges();
            TempData["Success"] = "Customer information updated successfully";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        [HttpPost]
        [Authorize(Roles = SD.Admin)]
        public IActionResult UpdateShippingInfo(int orderId, string streetAddress, string country, string postalCode)
        {
            var orderFromDb = _db.OrderHeaders.Find(orderId);
            if (orderFromDb == null)
            {
                return NotFound();
            }
            orderFromDb.StreetAddress = streetAddress;
            orderFromDb.Country = country;
            orderFromDb.PostalCode = postalCode;
            _db.SaveChanges();
            TempData["Success"] = "Shipping information updated successfully";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTracking(int id, string trackingNumber, string carrier)
        {
            try
            {
                var order = await _db.OrderHeaders.FindAsync(id);
                if (order == null)
                {
                    TempData["Error"] = "Order not found";
                    return RedirectToAction(nameof(Index));
                }

                order.TrackingNumber = trackingNumber;
                order.Carrier = carrier;

                await _db.SaveChangesAsync();

                TempData["Success"] = "Tracking information updated successfully";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error updating tracking information";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

    }
}
