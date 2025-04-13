using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;

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
        [HttpPost("Admin/Order/Approve/{id}")]
        [Authorize(Roles = SD.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string returnUrl = null)
        {
            var orderFromDb = await _db.OrderHeaders
                .Include(o => o.ApplicationUser)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (orderFromDb == null)
            {
                TempData["Error"] = "Order not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                orderFromDb.OrderStatus = SD.StatusApproved;
                orderFromDb.PaymentStatus = SD.PaymentStatusApproved;
                orderFromDb.PaymentDate = DateTime.Now;

                await _db.SaveChangesAsync();

                TempData["Success"] = $"Order #{orderFromDb.Id} approved successfully";
                return Redirect(returnUrl ?? Url.Action(nameof(Index), new { id }));

            }
            catch (Exception ex)
            {
                // Log the error
                TempData["Error"] = "Error approving order. Please try again.";
                return Redirect(returnUrl ?? Url.Action(nameof(Index), new { id }));

            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = SD.Admin)]
        public async Task<IActionResult> Cancel(int id, string returnUrl = null)
        {
            try
            {
                var orderHeader = await _db.OrderHeaders
                    .Include(o => o.ApplicationUser)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orderHeader == null)
                {
                    TempData["Error"] = "Order not found";
                    return Redirect(returnUrl ?? Url.Action(nameof(Index)));
                }

                // Validate order can be cancelled
                if (orderHeader.OrderStatus != SD.StatusPending &&
                    orderHeader.OrderStatus != SD.StatusApproved)
                {
                    TempData["Error"] = $"Cannot cancel order with status: {orderHeader.OrderStatus}";
                    return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
                }

                // Process refund if needed
                if (!string.IsNullOrEmpty(orderHeader.PaymentIntentId))
                {
                    var refundService = new RefundService();
                    var refundOptions = new RefundCreateOptions
                    {
                        PaymentIntent = orderHeader.PaymentIntentId,
                        Reason = RefundReasons.RequestedByCustomer
                    };

                    try
                    {
                        var refund = await refundService.CreateAsync(refundOptions);
                        orderHeader.PaymentStatus = SD.StatusRefunded;
                    }
                    catch (StripeException)
                    {
                        TempData["Error"] = "Refund failed. Please check Stripe dashboard.";
                        return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
                    }
                }

                // Update order status
                orderHeader.OrderStatus = SD.StatusCancelled;
                await _db.SaveChangesAsync();

                TempData["Success"] = $"Order #{orderHeader.Id} cancelled successfully";
                return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error cancelling order";
                return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ShipOrder(int id)
        {
            try
            {
                var order = await _db.OrderHeaders.FindAsync(id);
                if (order == null)
                {
                    TempData["Error"] = "Order not found";
                    return RedirectToAction(nameof(Index));
                }

                if (order.OrderStatus != SD.StatusApproved)
                {
                    TempData["Error"] = "Only approved orders can be shipped";
                    return RedirectToAction(nameof(Index), new { id });
                }

                order.OrderStatus = SD.StatusShipped;
                order.ShippingDate = DateTime.Now;

                await _db.SaveChangesAsync();

                TempData["Success"] = $"Order #{order.Id} shipped successfully";
                return RedirectToAction(nameof(Index), new { id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error shipping order. Please try again.";
                return RedirectToAction(nameof(Index), new { id });
            }
        }
            [HttpPost]
            [Authorize(Roles = SD.Admin)]
            [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCanceledRefundedOrders(int id, string returnUrl = null)
            {
                try
                {
                    var orderToRemove = await _db.OrderHeaders
                        .FirstOrDefaultAsync(o => o.Id == id &&
                                               (o.OrderStatus == SD.StatusCancelled ||
                                                o.OrderStatus == SD.StatusRefunded));

                    if (orderToRemove == null)
                    {
                        TempData["Error"] = "Order not found or not eligible for removal";
                        return RedirectToAction(nameof(Details), new { id });
                    }

                    // Remove order details first
                    var orderDetails = await _db.OrderDetails
                        .Where(od => od.OrderHeaderId == id)
                        .ToListAsync();

                    if (orderDetails.Any())
                    {
                        _db.OrderDetails.RemoveRange(orderDetails);
                    }

                    _db.OrderHeaders.Remove(orderToRemove);
                    await _db.SaveChangesAsync();

                    TempData["Success"] = $"Successfully removed order #{orderToRemove.Id}";

                    // Handle return URL more intelligently
                    if (!string.IsNullOrEmpty(returnUrl) && returnUrl.Contains("/Order/Details/"))
                    {
                        // If coming from Details page, redirect to Index since the order no longer exists
                        return RedirectToAction(nameof(Index));
                    }

                    return Redirect(returnUrl ?? Url.Action(nameof(Index)));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = "Error removing order - please try again";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }
        }
}
