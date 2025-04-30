using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
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
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;

        // Constructor initializes database context, email sender, and user manager
        public OrderController(ApplicationDbContext db, IEmailSender emailSender, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        // Retrieves all orders with user data, sorted by newest first
        public IActionResult Index()
        {
            var orderHeaders = _db.OrderHeaders
                .Include(a => a.ApplicationUser)
                .OrderByDescending(o => o.Id)
                .ToList();

            return View(orderHeaders);
        }

        // Retrieves order details by ID
        public IActionResult Details(int id)
        {
            var orderVM = new OrderVM
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

        // Updates customer information (name and phone number)
        [HttpPost]
        public IActionResult UpdateCustomerInfo(int orderId, string name, string phoneNumber)
        {
            var order = _db.OrderHeaders.Find(orderId);
            if (order == null)
                return NotFound();

            order.Name = name;
            order.PhoneNumber = phoneNumber;
            _db.SaveChanges();

            TempData["Success"] = "Customer information updated successfully!";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        // Updates shipping information (address, country, postal code)
        [HttpPost]
        public IActionResult UpdateShippingInfo(int orderId, string streetAddress, string country, string postalCode)
        {
            var order = _db.OrderHeaders.Find(orderId);
            if (order == null)
                return NotFound();

            order.StreetAddress = streetAddress;
            order.Country = country;
            order.PostalCode = postalCode;
            _db.SaveChanges();

            TempData["Success"] = "Shipping information updated successfully!";
            return RedirectToAction(nameof(Details), new { id = orderId });
        }

        // Updates tracking information (tracking number and carrier)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateTracking(int id, string trackingNumber, string carrier)
        {
            var order = _db.OrderHeaders.Find(id);
            if (order == null)
            {
                TempData["Error"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            order.TrackingNumber = trackingNumber;
            order.Carrier = carrier;
            _db.SaveChanges();

            TempData["Success"] = "Tracking information updated successfully!";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Approves an order and updates stock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(int id, string returnUrl = null)
        {
            var order = _db.OrderHeaders
                .Include(o => o.ApplicationUser)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            var orderDetails = _db.OrderDetails
                .Where(od => od.OrderHeaderId == id)
                .ToList();

            using var transaction = _db.Database.BeginTransaction();
            try
            {
                foreach (var detail in orderDetails)
                {
                    var stock = _db.Stocks
                        .FirstOrDefault(s => s.Product_Id == detail.ProductId && s.Size == detail.Size);

                    if (stock == null)
                        throw new Exception($"Stock not found for Product ID {detail.ProductId} with size {detail.Size}.");

                    if (stock.Quantity < detail.Count)
                        throw new Exception($"Insufficient stock for Product ID {detail.ProductId} (Size: {detail.Size}). Available: {stock.Quantity}, Requested: {detail.Count}.");

                    stock.Quantity -= detail.Count;
                    _db.Stocks.Update(stock);
                }

                order.OrderStatus = SD.StatusApproved;
                order.PaymentStatus = SD.PaymentStatusApproved;
                order.PaymentDate = DateTime.Now;

                _db.SaveChanges();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                order.OrderStatus = "Failed";
                order.PaymentStatus = "Failed";
                _db.SaveChanges();

                TempData["Error"] = $"Order approval failed: {ex.Message}";
                return Redirect(returnUrl ?? Url.Action(nameof(Index)));
            }

            var user = order.ApplicationUser;
            if (user != null)
            {
                var emailBody = GenerateEmailInProcess(user, order.Id);
                _emailSender.SendEmailAsync(user.Email, "Your Order Has Been Approved", emailBody).Wait();
            }
            else
            {
                TempData["Error"] = $"User for order #{order.Id} not found!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Order #{order.Id} approved successfully!";
            return Redirect(returnUrl ?? Url.Action(nameof(Index)));
        }

        // Cancels an order, updates stock, and processes refund if needed
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(int id, string returnUrl = null)
        {
            var order = _db.OrderHeaders
                .Include(o => o.ApplicationUser)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Order not found!";
                return Redirect(returnUrl ?? Url.Action(nameof(Index)));
            }

            if (order.OrderStatus != SD.StatusPending && order.OrderStatus != SD.StatusApproved)
            {
                TempData["Error"] = $"Cannot cancel order with status: {order.OrderStatus}";
                return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
            }

            var orderDetails = _db.OrderDetails
                .Where(od => od.OrderHeaderId == id)
                .ToList();

            using var transaction = _db.Database.BeginTransaction();
            try
            {
                if (order.OrderStatus == SD.StatusApproved)
                {
                    foreach (var detail in orderDetails)
                    {
                        var stock = _db.Stocks
                            .FirstOrDefault(s => s.Product_Id == detail.ProductId && s.Size == detail.Size);

                        if (stock == null)
                            throw new Exception($"Stock not found for Product ID {detail.ProductId} with size {detail.Size}.");

                        stock.Quantity += detail.Count;
                        _db.Stocks.Update(stock);
                    }
                }

                if (!string.IsNullOrEmpty(order.PaymentIntentId))
                {
                    var refundService = new RefundService();
                    var refundOptions = new RefundCreateOptions
                    {
                        PaymentIntent = order.PaymentIntentId,
                        Reason = RefundReasons.RequestedByCustomer
                    };

                    try
                    {
                        refundService.Create(refundOptions);
                        order.PaymentStatus = SD.StatusRefunded;
                    }
                    catch (StripeException)
                    {
                        throw new Exception("Refund failed. Please check Stripe dashboard.");
                    }
                }

                order.OrderStatus = SD.StatusCancelled;
                _db.SaveChanges();
                transaction.Commit();
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                TempData["Error"] = $"Order cancellation failed: {ex.Message}";
                return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
            }

            var user = order.ApplicationUser;
            if (user != null)
            {
                var emailBody = GenerateEmailCancelled(user, order.Id);
                _emailSender.SendEmailAsync(user.Email, "Your Order Has Been Cancelled", emailBody).Wait();
            }
            else
            {
                TempData["Error"] = $"User for order #{order.Id} not found!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Order #{order.Id} cancelled successfully!";
            return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
        }

        // Marks an order as shipped
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ShipOrder(int id)
        {
            var order = _db.OrderHeaders
                .Include(o => o.ApplicationUser)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            if (order.OrderStatus != SD.StatusApproved)
            {
                TempData["Error"] = "Only approved orders can be shipped!";
                return RedirectToAction(nameof(Index));
            }

            order.OrderStatus = SD.StatusShipped;
            order.ShippingDate = DateTime.Now;
            _db.SaveChanges();

            var user = order.ApplicationUser;
            if (user != null)
            {
                var emailBody = GenerateEmailShipped(user, order.Id);
                _emailSender.SendEmailAsync(user.Email, "Your Order Has Been Shipped", emailBody).Wait();
            }
            else
            {
                TempData["Error"] = $"User for order #{order.Id} not found!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Order #{order.Id} shipped successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Marks an order as delivered
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAsDelivered(int id, string returnUrl = null)
        {
            var order = _db.OrderHeaders
                .Include(o => o.ApplicationUser)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                TempData["Error"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            if (order.OrderStatus != SD.StatusShipped)
            {
                TempData["Error"] = "Cannot mark as delivered unless order is shipped!";
                return RedirectToAction(nameof(Index));
            }

            order.OrderStatus = SD.StatusDelivered;
            _db.OrderHeaders.Update(order);
            _db.SaveChanges();

            var user = order.ApplicationUser;
            if (user != null)
            {
                var emailBody = GenerateEmailDelivered(user, order.Id);
                _emailSender.SendEmailAsync(user.Email, "Your Order Has Been Delivered", emailBody).Wait();
            }
            else
            {
                TempData["Error"] = $"User for order #{order.Id} not found!";
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = $"Order #{id} delivered successfully!";
            return Redirect(returnUrl ?? Url.Action(nameof(Index)));
        }

        // Removes cancelled or refunded orders
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveCanceledRefundedOrders(int id, string returnUrl = null)
        {
            var order = _db.OrderHeaders
                .FirstOrDefault(o => o.Id == id &&
                                     (o.OrderStatus == SD.StatusCancelled ||
                                      o.OrderStatus == SD.StatusRefunded));

            if (order == null)
            {
                TempData["Error"] = "Order not found or not eligible for removal!";
                return RedirectToAction(nameof(Details), new { id });
            }

            var orderDetails = _db.OrderDetails
                .Where(od => od.OrderHeaderId == id)
                .ToList();

            if (orderDetails.Any())
                _db.OrderDetails.RemoveRange(orderDetails);

            _db.OrderHeaders.Remove(order);
            _db.SaveChanges();

            TempData["Success"] = $"Successfully removed order #{order.Id}!";

            if (!string.IsNullOrEmpty(returnUrl) && returnUrl.Contains("/Order/Details/"))
                return RedirectToAction(nameof(Index));

            return Redirect(returnUrl ?? Url.Action(nameof(Index)));
        }

        // Generates HTML email for delivered order
        private string GenerateEmailDelivered(ApplicationUser user, int orderNumber)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 1px solid #eaeaea; padding-bottom: 15px; }}
        .header h1 {{ color: #088178; margin: 0; font-size: 24px; }}
        .content {{ margin-bottom: 25px; line-height: 1.6; }}
        .content p {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .order-number {{ font-size: 28px; font-weight: bold; color: #088178; letter-spacing: 3px; text-align: center; margin: 25px 0; padding: 15px; background-color: #f8f9fa; border-radius: 6px; border: 1px dashed #088178; }}
        .security-alert {{ background-color: #f8f9fa; border-left: 4px solid #088178; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; font-size: 14px; color: #777; margin-top: 25px; border-top: 1px solid #eaeaea; padding-top: 15px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #088178; color: white; text-decoration: none; border-radius: 4px; margin: 20px auto; text-align: center; }}
        .info-item {{ margin-bottom: 8px; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'><h1>Order Delivered</h1></div>
        <div class='content'>
            <p>Hello {user.Name},</p>
            <p>Your order has been successfully delivered. We hope you enjoy your purchase!</p>
            <div class='order-number'>Order Number: {orderNumber}</div>
            <p>If you have any questions, feel free to reach out to us.</p>
            <div class='security-alert'><p><strong>Security Tip:</strong> Ensure communications are directly from LushThreads.</p></div>
            <p>Track your order status:</p>
            <a href='{GenerateEmailLink(user)}' class='button'>Track Your Order</a>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all;'>{GenerateEmailLink(user)}</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our order delivery process.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates HTML email for shipped order
        private string GenerateEmailShipped(ApplicationUser user, int orderNumber)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 1px solid #eaeaea; padding-bottom: 15px; }}
        .header h1 {{ color: #088178; margin: 0; font-size: 24px; }}
        .content {{ margin-bottom: 25px; line-height: 1.6; }}
        .content p {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .order-number {{ font-size: 28px; font-weight: bold; color: #088178; letter-spacing: 3px; text-align: center; margin: 25px 0; padding: 15px; background-color: #f8f9fa; border-radius: 6px; border: 1px dashed #088178; }}
        .security-alert {{ background-color: #f8f9fa; border-left: 4px solid #088178; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; font-size: 14px; color: #777; margin-top: 25px; border-top: 1px solid #eaeaea; padding-top: 15px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #088178; color: white; text-decoration: none; border-radius: 4px; margin: 20px auto; text-align: center; }}
        .info-item {{ margin-bottom: 8px; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'><h1>Your Order Has Shipped!</h1></div>
        <div class='content'>
            <p>Hello {user.Name},</p>
            <p>Your order has been shipped and is on its way!</p>
            <div class='order-number'>Order Number: {orderNumber}</div>
            <p>What's next:</p>
            <ul>
                <li>Your package is with our shipping partner</li>
                <li>You'll receive tracking updates</li>
                <li>Check estimated delivery in tracking info</li>
            </ul>
            <div class='security-alert'><p><strong>Security Tip:</strong> Ensure communications are directly from LushThreads.</p></div>
            <p>Track your shipment:</p>
            <a href='{GenerateEmailLink(user)}' class='button'>Track Your Package</a>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all;'>{GenerateEmailLink(user)}</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our order shipping process.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates HTML email for order in process
        private string GenerateEmailInProcess(ApplicationUser user, int orderNumber)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 1px solid #eaeaea; padding-bottom: 15px; }}
        .header h1 {{ color: #088178; margin: 0; font-size: 24px; }}
        .content {{ margin-bottom: 25px; line-height: 1.6; }}
        .content p {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .order-number {{ font-size: 28px; font-weight: bold; color: #088178; letter-spacing: 3px; text-align: center; margin: 25px 0; padding: 15px; background-color: #e6f4f1; border-radius: 6px; border: 1px dashed #088178; }}
        .info-box {{ background-color: #e6f4f1; border-left: 4px solid #088178; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; font-size: 14px; color: #777; margin-top: 25px; border-top: 1px solid #eaeaea; padding-top: 15px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #088178; color: white; text-decoration: none; border-radius: 4px; margin: 20px auto; text-align: center; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'><h1>Your Order Is Being Processed</h1></div>
        <div class='content'>
            <p>Hello {user.Name},</p>
            <p>Your order is being processed. Thank you for your purchase!</p>
            <div class='order-number'>Order #{orderNumber}</div>
            <p>What's next:</p>
            <ul>
                <li>Your items are being prepared for shipment</li>
                <li>You'll receive a shipping confirmation soon</li>
                <li>Track your order status from your account</li>
            </ul>
            <div class='info-box'><p><strong>Need help?</strong> Reply to this email or contact our support team.</p></div>
            <p>Track your order status:</p>
            <a href='{GenerateEmailLink(user)}' class='button'>View Order Status</a>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all;'>{GenerateEmailLink(user)}</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} regarding your recent order.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates HTML email for cancelled order
        private string GenerateEmailCancelled(ApplicationUser user, int orderNumber)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 1px solid #eaeaea; padding-bottom: 15px; }}
        .header h1 {{ color: #088178; margin: 0; font-size: 24px; }}
        .content {{ margin-bottom: 25px; line-height: 1.6; }}
        .content p {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .order-number {{ font-size: 28px; font-weight: bold; color: #088178; letter-spacing: 3px; text-align: center; margin: 25px 0; padding: 15px; background-color: #e6f4f1; border-radius: 6px; border: 1px dashed #088178; }}
        .info-box {{ background-color: #e6f4f1; border-left: 4px solid #088178; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; font-size: 14px; color: #777; margin-top: 25px; border-top: 1px solid #eaeaea; padding-top: 15px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #088178; color: white; text-decoration: none; border-radius: 4px; margin: 20px auto; text-align: center; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'><h1>Your Order Has Been Cancelled</h1></div>
        <div class='content'>
            <p>Hello {user.Name},</p>
            <p>Your order has been cancelled. Details below:</p>
            <div class='order-number'>Order #{orderNumber}</div>
            <p>Possible reasons for cancellation:</p>
            <ul>
                <li>Items no longer available</li>
                <li>Payment not completed</li>
                <li>Cancelled at your request</li>
            </ul>
            <div class='info-box'><p><strong>Need help?</strong> Contact our support team.</p></div>
            <p>View your order history:</p>
            <a href='{GenerateEmailLink(user)}' class='button'>View My Orders</a>
            <p>If the button doesn't work, copy and paste this link:</p>
            <p style='word-break: break-all;'>{GenerateEmailLink(user)}</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} regarding your cancelled order.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates link to order tracking page
        private string GenerateEmailLink(ApplicationUser user)
        {
            return Url.Action("Orders", "Profile", new { area = "Customer", userId = user.Id }, HttpContext.Request.Scheme);
        }
    }
}