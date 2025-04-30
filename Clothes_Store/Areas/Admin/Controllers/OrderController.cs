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
using Stripe.Climate;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext db, IEmailSender emailSender, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _emailSender = emailSender;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            List<OrderHeader> objOrderHeaders = _db.OrderHeaders
                .Include(a => a.ApplicationUser)
                .OrderByDescending(o => o.Id)
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

        [HttpPost]
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
                // Load order details to update stock
                var orderDetails = await _db.OrderDetails
                    .Where(od => od.OrderHeaderId == id)
                    .ToListAsync();

                // Begin transaction to ensure stock updates are atomic
                using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    // Decrease stock for each order detail
                    foreach (var detail in orderDetails)
                    {
                        var stock = await _db.Stocks
                            .FirstOrDefaultAsync(s => s.Product_Id == detail.ProductId && s.Size == detail.Size);

                        if (stock == null)
                        {
                            throw new Exception($"Stock not found for Product ID {detail.ProductId} with size {detail.Size}.");
                        }

                        if (stock.Quantity < detail.Count)
                        {
                            throw new Exception($"Insufficient stock for Product ID {detail.ProductId} (Size: {detail.Size}). Available: {stock.Quantity}, Requested: {detail.Count}.");
                        }

                        stock.Quantity -= detail.Count;
                        _db.Stocks.Update(stock);
                    }

                    // Update order status
                    orderFromDb.OrderStatus = SD.StatusApproved;
                    orderFromDb.PaymentStatus = SD.PaymentStatusApproved;
                    orderFromDb.PaymentDate = DateTime.Now;

                    await _db.SaveChangesAsync();

                    // Commit transaction after successful stock update
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    // Rollback transaction on failure
                    await transaction.RollbackAsync();

                    // Update order status to failed
                    orderFromDb.OrderStatus = "Failed";
                    orderFromDb.PaymentStatus = "Failed";
                    await _db.SaveChangesAsync();

                    TempData["Error"] = $"Order approval failed: {ex.Message}";
                    return Redirect(returnUrl ?? Url.Action(nameof(Index), new { id }));
                }

                var user = orderFromDb.ApplicationUser;

                if (user != null)
                {
                    var emailBody = GenerateEmailInProcess(user, orderFromDb.Id);

                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Your Order Has Been Approved",
                        emailBody
                    );
                }
                else
                {
                    TempData["Error"] = $"User for order #{orderFromDb.Id} not found!";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = $"Order #{orderFromDb.Id} approved successfully";
                return Redirect(returnUrl ?? Url.Action(nameof(Index), new { id }));
            }
            catch (Exception ex)
            {
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

                // Load order details to update stock
                var orderDetails = await _db.OrderDetails
                    .Where(od => od.OrderHeaderId == id)
                    .ToListAsync();

                // Begin transaction to ensure stock updates are atomic
                using var transaction = await _db.Database.BeginTransactionAsync();

                try
                {
                    // Increase stock for each order detail (if it was previously decreased)
                    if (orderHeader.OrderStatus == SD.StatusApproved)
                    {
                        foreach (var detail in orderDetails)
                        {
                            var stock = await _db.Stocks
                                .FirstOrDefaultAsync(s => s.Product_Id == detail.ProductId && s.Size == detail.Size);

                            if (stock == null)
                            {
                                throw new Exception($"Stock not found for Product ID {detail.ProductId} with size {detail.Size}.");
                            }

                            stock.Quantity += detail.Count;
                            _db.Stocks.Update(stock);
                        }
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
                            throw new Exception("Refund failed. Please check Stripe dashboard.");
                        }
                    }

                    // Update order status
                    orderHeader.OrderStatus = SD.StatusCancelled;
                    await _db.SaveChangesAsync();

                    // Commit transaction after successful stock update
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    // Rollback transaction on failure
                    await transaction.RollbackAsync();

                    TempData["Error"] = $"Order cancellation failed: {ex.Message}";
                    return Redirect(returnUrl ?? Url.Action(nameof(Details), new { id }));
                }

                var user = orderHeader.ApplicationUser;

                if (user != null)
                {
                    var emailBody = GenerateEmailCancelled(user, orderHeader.Id);

                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Your Order Has Been Cancelled",
                        emailBody
                    );
                }
                else
                {
                    TempData["Error"] = $"User for order #{orderHeader.Id} not found!";
                    return RedirectToAction(nameof(Index));
                }

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
                var order = await _db.OrderHeaders
                    .Include(o => o.ApplicationUser)
                    .FirstOrDefaultAsync(o => o.Id == id);
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

                var user = order.ApplicationUser;

                if (user != null)
                {
                    var emailBody = GenerateEmailShipped(user, order.Id);

                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Your Order Has Been Shipped",
                        emailBody
                    );
                }
                else
                {
                    TempData["Error"] = $"User for order #{order.Id} not found!";
                    return RedirectToAction(nameof(Index));
                }
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsDelivered(int id, string returnUrl = null)
        {
            try
            {
                var orderFromDb = await _db.OrderHeaders
                    .Include(o => o.ApplicationUser)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orderFromDb == null)
                {
                    TempData["Error"] = "Order not found!";
                    return RedirectToAction(nameof(Index));
                }

                if (orderFromDb.OrderStatus != SD.StatusShipped)
                {
                    TempData["Error"] = "Cannot mark the order as 'Delivered' unless its status is 'Shipped'";
                    return RedirectToAction(nameof(Index));
                }

                orderFromDb.OrderStatus = SD.StatusDelivered;
                _db.OrderHeaders.Update(orderFromDb);
                await _db.SaveChangesAsync();

                var user = orderFromDb.ApplicationUser;

                if (user != null)
                {
                    var emailBody = GenerateEmailDelivered(user, orderFromDb.Id);

                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Your Order Has Been Delivered",
                        emailBody
                    );
                }
                else
                {
                    TempData["Error"] = $"User for order #{orderFromDb.Id} not found!";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = $"Order #{id} Delivered successfully";
                return Redirect(returnUrl ?? Url.Action(nameof(Index)));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"An error occurred: {ex.Message}";
                return RedirectToAction(nameof(Index));
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

                if (!string.IsNullOrEmpty(returnUrl) && returnUrl.Contains("/Order/Details/"))
                {
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

        private string GenerateEmailDelivered(ApplicationUser user, int orderNumber)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f4;
            margin: 0;
            padding: 20px;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 25px;
            border-bottom: 1px solid #eaeaea;
            padding-bottom: 15px;
        }}
        .header h1 {{
            color: #088178;
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            margin-bottom: 25px;
            line-height: 1.6;
        }}
        .content p {{
            font-size: 16px;
            color: #333333;
            margin-bottom: 15px;
        }}
        .order-number {{
            font-size: 28px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .security-alert {{
            background-color: #f8f9fa;
            border-left: 4px solid #088178;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #777;
            margin-top: 25px;
            border-top: 1px solid #eaeaea;
            padding-top: 15px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #088178;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px auto;
            text-align: center;
        }}
        .info-item {{
            margin-bottom: 8px;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Order Just Delivered</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>We are happy to inform you that your order has been successfully delivered. We hope you enjoy your purchase!</p>
            
            <div class='order-number'>
                Order Number: {orderNumber}
            </div>
            
            <p>If you have any questions regarding your order, feel free to reach out to us.</p>
            
            <div class='security-alert'>
                <p><strong>Security Tip:</strong> Always ensure that you are receiving communications directly from Cara Store.</p>
            </div>
            
            <p>You can track your order status by clicking the button below:</p>
            
            <a href=""{GenerateEmailLink(user)}"" class='button'>Track Your Order</a>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateEmailLink(user)}</p>

        </div>
        
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} Cara Store. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our order delivery process.</p>
        </div>
    </div>
</body>
</html>";
        }
        private string GenerateEmailShipped(ApplicationUser user, int orderNumber)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f4;
            margin: 0;
            padding: 20px;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 25px;
            border-bottom: 1px solid #eaeaea;
            padding-bottom: 15px;
        }}
        .header h1 {{
            color: #088178;
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            margin-bottom: 25px;
            line-height: 1.6;
        }}
        .content p {{
            font-size: 16px;
            color: #333333;
            margin-bottom: 15px;
        }}
        .order-number {{
            font-size: 28px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .security-alert {{
            background-color: #f8f9fa;
            border-left: 4px solid #088178;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #777;
            margin-top: 25px;
            border-top: 1px solid #eaeaea;
            padding-top: 15px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #088178;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px auto;
            text-align: center;
        }}
        .info-item {{
            margin-bottom: 8px;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Your Order Has Shipped!</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>Great news! Your order has been shipped and is on its way to you. We can't wait for you to receive it!</p>
            
            <div class='order-number'>
                Order Number: {orderNumber}
            </div>
            
            <p>Here's what you can expect next:</p>
            <ul>
                <li>Your package is now with our shipping partner</li>
                <li>You'll receive tracking updates as it moves through the delivery network</li>
                <li>Estimated delivery date will be available in your tracking information</li>
            </ul>
            
            <div class='security-alert'>
                <p><strong>Security Tip:</strong> Always ensure that you are receiving communications directly from Cara Store.</p>
            </div>
            
            <p>Click the button below to track your shipment:</p>
            
            <a href=""{GenerateEmailLink(user)}"" class='button'>Track Your Package</a>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateEmailLink(user)}</p>

        </div>
        
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} Cara Store. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our order shipping process.</p>
        </div>
    </div>
</body>
</html>";
        }
        private string GenerateEmailInProcess(ApplicationUser user, int orderNumber)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f4;
            margin: 0;
            padding: 20px;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 25px;
            border-bottom: 1px solid #eaeaea;
            padding-bottom: 15px;
        }}
        .header h1 {{
            color: #088178;
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            margin-bottom: 25px;
            line-height: 1.6;
        }}
        .content p {{
            font-size: 16px;
            color: #333333;
            margin-bottom: 15px;
        }}
        .order-number {{
            font-size: 28px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #e6f4f1;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .info-box {{
            background-color: #e6f4f1;
            border-left: 4px solid #088178;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #777;
            margin-top: 25px;
            border-top: 1px solid #eaeaea;
            padding-top: 15px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #088178;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px auto;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Your Order Is Being Processed</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>Thank you for your purchase! We're excited to let you know that your order is currently being processed by our team.</p>
            
            <div class='order-number'>
                Order #{orderNumber}
            </div>
            
            <p>What happens next?</p>
            <ul>
                <li>We're preparing your items for shipment</li>
                <li>Once ready, you'll receive a shipping confirmation email</li>
                <li>You can track your order status from your account at any time</li>
            </ul>
            
            <div class='info-box'>
                <p><strong>Need help?</strong> You can reply to this email or contact our support team at any time.</p>
            </div>

            <p>You can also track your order status through your account:</p>
            
            <p style=""text-align:center; margin-top:20px;"">
                <a href=""{GenerateEmailLink(user)}"" class='button'>View Order Status</a>
            </p>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateEmailLink(user)}</p>
        </div>
        
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} Cara Store. All rights reserved.</p>
            <p>This email was sent to {user.Email} regarding the processing of your recent order.</p>
        </div>
    </div>
</body>
</html>";
        }
        private string GenerateEmailCancelled(ApplicationUser user, int orderNumber)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f4;
            margin: 0;
            padding: 20px;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 25px;
            border-bottom: 1px solid #eaeaea;
            padding-bottom: 15px;
        }}
        .header h1 {{
            color: #088178;
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            margin-bottom: 25px;
            line-height: 1.6;
        }}
        .content p {{
            font-size: 16px;
            color: #333333;
            margin-bottom: 15px;
        }}
        .order-number {{
            font-size: 28px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #e6f4f1;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .info-box {{
            background-color: #e6f4f1;
            border-left: 4px solid #088178;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #777;
            margin-top: 25px;
            border-top: 1px solid #eaeaea;
            padding-top: 15px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #088178;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px auto;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Your Order Has Been Cancelled</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>We regret to inform you that your order has been cancelled. Please find the details below:</p>
            
            <div class='order-number'>
                Order #{orderNumber}
            </div>
            
            <p>Possible reasons for cancellation:</p>
            <ul>
                <li>One or more items are no longer available</li>
                <li>Payment was not completed</li>
                <li>The order was cancelled at your request</li>
            </ul>
            
            <div class='info-box'>
                <p>If you think this was a mistake or you need further assistance, please contact our support team.</p>
            </div>

            <p>You can view your order history through your account:</p>

            <p style=""text-align:center; margin-top:20px;"">
                <a href=""{GenerateEmailLink(user)}"" class='button'>View My Orders</a>
            </p>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateEmailLink(user)}</p>
        </div>
        
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} Cara Store. All rights reserved.</p>
            <p>This email was sent to {user.Email} regarding your cancelled order.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateEmailLink(ApplicationUser user)
        {
            return Url.Action(
                "Orders",
                "Profile",
                new { area = "Customer", userId = user.Id },
                protocol: HttpContext.Request.Scheme
            );
        }
    }
}
