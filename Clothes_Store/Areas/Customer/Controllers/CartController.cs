using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : BaseController
    {
        public CartController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : base(db, userManager)
        {
        }

        // Displays the user's shopping cart
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.CartCount = await _db.CartItems.Where(c => c.UserId == user.Id).CountAsync();

            var cartItems = await _db.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Stocks)
                .Where(ci => ci.UserId == user.Id)
                .ToListAsync();

            var (subtotal, shippingFee, tax, total) = CalculateOrderTotals(cartItems);

            var viewModel = new CartViewModel
            {
                Items = cartItems,
                OrderHeader = new OrderHeader
                {
                    OrderDate = DateTime.Now,
                    ShippingDate = DateTime.Now.AddDays(3),
                    Subtotal = subtotal,
                    ShippingFee = shippingFee,
                    Tax = tax,
                    OrderTotal = total,
                    Name = user.Name,
                    PhoneNumber = user.PhoneNumber,
                    StreetAddress = user.StreetAddress,
                    Country = user.Country,
                    PostalCode = user.PostalCode
                }
            };

            return View(viewModel);
        }

        // Adds an item to the cart with size validation
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1, string size = null)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var product = await _db.Products
                    .Include(p => p.Stocks)
                    .FirstOrDefaultAsync(p => p.Product_Id == productId);

                if (product == null)
                    return Json(new { success = false, message = "Product not found" });

                // Validate or assign default size
                if (!string.IsNullOrEmpty(size))
                {
                    var availableSize = product.Stocks?.Any(s => s.Size == size) ?? false;
                    if (!availableSize)
                        return Json(new { success = false, message = "Selected size is not available" });
                }
                else
                {
                    size = product.Stocks?.FirstOrDefault()?.Size;
                    if (string.IsNullOrEmpty(size))
                        return Json(new { success = false, message = "No sizes available for this product" });
                }

                var existingItem = await _db.CartItems
                    .FirstOrDefaultAsync(ci => ci.ProductId == productId && ci.UserId == user.Id && ci.Size == size);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    _db.CartItems.Add(new CartItem
                    {
                        ProductId = productId,
                        UserId = user.Id,
                        Quantity = quantity,
                        Size = size
                    });
                }

                await _db.SaveChangesAsync();

                var cartCount = await _db.CartItems
                    .Where(ci => ci.UserId == user.Id)
                    .SumAsync(ci => ci.Quantity);

                return Json(new { success = true, message = "Item added to cart successfully!", cartCount });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Removes an item from the cart
        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var cartItem = await _db.CartItems
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == id);

            if (cartItem != null)
            {
                _db.CartItems.Remove(cartItem);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Increases the quantity of a cart item
        [HttpPost]
        public async Task<IActionResult> IncreaseQuantity(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItem = await _db.CartItems
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == user.Id);

            if (cartItem == null)
                return NotFound();

            cartItem.Quantity += 1;
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Decreases the quantity of a cart item or removes it if quantity becomes 0
        [HttpPost]
        public async Task<IActionResult> DecreaseQuantity(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItem = await _db.CartItems
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == user.Id);

            if (cartItem == null)
                return NotFound();

            if (cartItem.Quantity > 1)
            {
                cartItem.Quantity -= 1;
            }
            else
            {
                _db.CartItems.Remove(cartItem);
            }

            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Clears all items from the user's cart
        [HttpPost]
        public async Task<IActionResult> ClearCart()
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _db.CartItems
                .Where(ci => ci.UserId == user.Id)
                .ToListAsync();

            _db.CartItems.RemoveRange(cartItems);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // Updates the size of a cart item
        [HttpPost]
        public async Task<IActionResult> UpdateSize(int itemId, string size)
        {
            if (!User.Identity.IsAuthenticated)
                return Json(new { success = false, message = "User not authenticated. Please log in." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "User not found. Please log in again." });

            var cartItem = await _db.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Stocks)
                .FirstOrDefaultAsync(ci => ci.Id == itemId && ci.UserId == user.Id);

            if (cartItem == null)
                return Json(new { success = false, message = $"Cart item with ID {itemId} not found for user {user.Id}." });

            if (string.IsNullOrEmpty(size))
                return Json(new { success = false, message = "Please select a valid size." });

            var availableSize = cartItem.Product.Stocks?.Any(s => s.Size == size) ?? false;
            if (!availableSize)
                return Json(new { success = false, message = $"Selected size '{size}' is not available for product {cartItem.Product.Product_Name}." });

            var stock = cartItem.Product.Stocks.FirstOrDefault(s => s.Size == size);
            if (stock != null && stock.Quantity < cartItem.Quantity)
                return Json(new { success = false, message = $"Insufficient stock for size '{size}'. Only {stock.Quantity} items available." });

            var existingItem = await _db.CartItems
                .FirstOrDefaultAsync(ci => ci.ProductId == cartItem.ProductId && ci.UserId == user.Id && ci.Size == size && ci.Id != itemId);

            if (existingItem != null)
            {
                existingItem.Quantity += cartItem.Quantity;
                _db.CartItems.Remove(cartItem);
            }
            else
            {
                cartItem.Size = size;
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Size updated successfully." });
        }

        // Updates the user's payment method
        [HttpPost]
        public async Task<IActionResult> ChangePayment(string PaymentMethod)
        {
            var user = await _userManager.GetUserAsync(User);
            try
            {
                if (user != null)
                {
                    user.PaymentMehtod = PaymentMethod;
                    await _userManager.UpdateAsync(user);
                    TempData["Success"] = "Payment method updated successfully!";
                }
                else
                {
                    TempData["Error"] = "User not found";
                }
            }
            catch (Exception)
            {
                TempData["Error"] = "Failed to update payment method";
            }

            return RedirectToAction(nameof(Summary));
        }

        // Displays the cart summary with order details
        [HttpGet]
        public async Task<IActionResult> Summary()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.CartCount = await _db.CartItems.Where(c => c.UserId == user.Id).CountAsync();

            var cartItems = await _db.CartItems
                .Include(ci => ci.Product)
                    .ThenInclude(p => p.Stocks)
                .Where(ci => ci.UserId == user.Id)
                .ToListAsync();

            var (subtotal, shippingFee, tax, total) = CalculateOrderTotals(cartItems);

            var viewModel = new CartViewModel
            {
                Items = cartItems,
                OrderHeader = new OrderHeader
                {
                    ApplicationUserId = user.Id,
                    OrderDate = DateTime.Now,
                    ShippingDate = DateTime.Now.AddDays(3),
                    Subtotal = subtotal,
                    ShippingFee = shippingFee,
                    Tax = tax,
                    OrderTotal = total,
                    OrderStatus = SD.StatusPending,
                    PaymentStatus = SD.PaymentStatusPending,
                    Name = user.Name,
                    PhoneNumber = user.PhoneNumber,
                    StreetAddress = user.StreetAddress,
                    Country = user.Country,
                    PostalCode = user.PostalCode
                },
                User = user
            };

            return View(viewModel);
        }

        // Processes the cart summary and initiates payment
        [HttpPost]
        [ActionName("Summary")]
        [Route("Customer/Cart/Summary")]
        public async Task<IActionResult> SummaryPOST(CartViewModel viewModel, string selectedPaymentMethod, bool isFinalSubmission = false)
        {
            var user = await _userManager.GetUserAsync(User);
            viewModel.User = user;

            if (!isFinalSubmission && !string.IsNullOrEmpty(selectedPaymentMethod))
            {
                var paymentMethod = await _db.PaymentMethods
                    .FirstOrDefaultAsync(p => p.UserId == user.Id && p.IsDefault)
                    ?? new Clothes_Models.Models.PaymentMethod
                    {
                        UserId = user.Id,
                        IsDefault = true,
                        CreatedAt = DateTime.UtcNow
                    };

                paymentMethod.CardBrand = selectedPaymentMethod == "credit" ? "card" : null;
                paymentMethod.Last4 = selectedPaymentMethod == "credit" ? "4242" : null;

                if (paymentMethod.Id == 0)
                    _db.PaymentMethods.Add(paymentMethod);
                else
                    _db.PaymentMethods.Update(paymentMethod);

                await _db.SaveChangesAsync();
                TempData["Success"] = "Payment method updated successfully!";
                return RedirectToAction(nameof(Summary));
            }

            var cartItems = await _db.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.UserId == user.Id)
                .ToListAsync();

            var (subtotal, shippingFee, tax, total) = CalculateOrderTotals(cartItems);

            viewModel.OrderHeader.OrderDate = DateTime.Now;
            viewModel.OrderHeader.ApplicationUserId = user.Id;
            viewModel.OrderHeader.OrderStatus = SD.StatusPending;
            viewModel.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            viewModel.OrderHeader.OrderTotal = total;
            viewModel.OrderHeader.Subtotal = subtotal;
            viewModel.OrderHeader.ShippingFee = shippingFee;
            viewModel.OrderHeader.Tax = tax;

            var paymentMethodForOrder = await _db.PaymentMethods
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.IsDefault)
                ?? new Clothes_Models.Models.PaymentMethod
                {
                    UserId = user.Id,
                    IsDefault = true,
                    CreatedAt = DateTime.UtcNow
                };

            paymentMethodForOrder.BillingName = viewModel.OrderHeader.Name;
            paymentMethodForOrder.PhoneNumber = viewModel.OrderHeader.PhoneNumber;
            paymentMethodForOrder.AddressLine1 = viewModel.OrderHeader.StreetAddress;
            paymentMethodForOrder.Country = viewModel.OrderHeader.Country;
            paymentMethodForOrder.PostalCode = viewModel.OrderHeader.PostalCode;

            if (paymentMethodForOrder.Id == 0)
                _db.PaymentMethods.Add(paymentMethodForOrder);

            _db.OrderHeaders.Add(viewModel.OrderHeader);
            await _db.SaveChangesAsync();

            foreach (var item in cartItems)
            {
                _db.OrderDetails.Add(new OrderDetail
                {
                    ProductId = item.ProductId,
                    OrderHeaderId = viewModel.OrderHeader.Id,
                    price = (double)item.Product.Product_Price,
                    Count = item.Quantity,
                    Size = item.Size
                });
            }
            await _db.SaveChangesAsync();

            if (selectedPaymentMethod == "credit")
            {
                var domain = "https://" + Request.Host.Value;
                var options = new SessionCreateOptions
                {
                    SuccessUrl = domain + $"/Customer/Cart/OrderConfirmation/{viewModel.OrderHeader.Id}?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = domain + "/Customer/Cart/Index",
                    Mode = "payment",
                    LineItems = cartItems.Select(item => new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Product.Product_Price * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Product.Product_Name
                            }
                        },
                        Quantity = item.Quantity
                    }).ToList()
                };

                if (shippingFee > 0)
                {
                    options.LineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(shippingFee * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Shipping Fee"
                            }
                        },
                        Quantity = 1
                    });
                }

                if (tax > 0)
                {
                    options.LineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(tax * 100),
                            Currency = "usd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Tax"
                            }
                        },
                        Quantity = 1
                    });
                }

                var service = new SessionService();
                Session session = service.Create(options);

                viewModel.OrderHeader.SessionId = session.Id;
                viewModel.OrderHeader.PaymentIntentId = session.PaymentIntentId;
                await _db.SaveChangesAsync();

                return Redirect(session.Url);
            }
            else
            {
                await _db.SaveChangesAsync();
                _db.CartItems.RemoveRange(cartItems);
                await _db.SaveChangesAsync();

                return RedirectToAction("OrderConfirmation", new { id = viewModel.OrderHeader.Id });
            }
        }

        // Confirms the order and updates stock and payment status
        public async Task<IActionResult> OrderConfirmation(int id, string session_id)
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.CartCount = await _db.CartItems.Where(c => c.UserId == user.Id).CountAsync();

            var orderHeader = await _db.OrderHeaders
                .Include(oh => oh.ApplicationUser)
                .FirstOrDefaultAsync(oh => oh.Id == id);

            if (orderHeader == null)
                return NotFound();

            var orderDetails = await _db.OrderDetails
                .Where(od => od.OrderHeaderId == id)
                .ToListAsync();

            if (!string.IsNullOrEmpty(session_id))
            {
                try
                {
                    var sessionService = new SessionService();
                    var session = sessionService.Get(session_id);

                    if (session.PaymentStatus == "paid")
                    {
                        using var transaction = await _db.Database.BeginTransactionAsync();
                        try
                        {
                            foreach (var detail in orderDetails)
                            {
                                var stock = await _db.Stocks
                                    .FirstOrDefaultAsync(s => s.Product_Id == detail.ProductId && s.Size == detail.Size);

                                if (stock == null)
                                    throw new Exception($"Stock not found for Product ID {detail.ProductId} with size {detail.Size}.");

                                if (stock.Quantity < detail.Count)
                                    throw new Exception($"Insufficient stock for Product ID {detail.ProductId} (Size: {detail.Size}). Available: {stock.Quantity}, Requested: {detail.Count}.");

                                stock.Quantity -= detail.Count;
                                _db.Stocks.Update(stock);
                            }

                            orderHeader.SessionId = session_id;
                            orderHeader.PaymentIntentId = session.PaymentIntentId;
                            orderHeader.PaymentStatus = SD.PaymentStatusApproved;
                            orderHeader.OrderStatus = SD.StatusApproved;

                            var paymentIntentService = new PaymentIntentService();
                            var paymentIntent = paymentIntentService.Get(session.PaymentIntentId);

                            var options = new PaymentMethodGetOptions { Expand = new List<string> { "card" } };
                            var stripePaymentMethod = new PaymentMethodService().Get(paymentIntent.PaymentMethodId, options);

                            var paymentMethod = await _db.PaymentMethods
                                .FirstOrDefaultAsync(p => p.StripePaymentMethodId == paymentIntent.PaymentMethodId)
                                ?? new Clothes_Models.Models.PaymentMethod
                                {
                                    UserId = user.Id,
                                    StripePaymentMethodId = paymentIntent.PaymentMethodId,
                                    StripeCustomerId = session.CustomerId,
                                    IsDefault = true,
                                    CreatedAt = DateTime.UtcNow
                                };

                            if (stripePaymentMethod?.Card != null)
                            {
                                paymentMethod.CardBrand = stripePaymentMethod.Card.Brand;
                                paymentMethod.Last4 = stripePaymentMethod.Card.Last4;
                                paymentMethod.ExpMonth = (int)stripePaymentMethod.Card.ExpMonth;
                                paymentMethod.ExpYear = (int)stripePaymentMethod.Card.ExpYear;
                            }

                            if (stripePaymentMethod?.BillingDetails != null)
                            {
                                paymentMethod.BillingName = stripePaymentMethod.BillingDetails.Name;
                                paymentMethod.PhoneNumber = stripePaymentMethod.BillingDetails.Phone;
                                paymentMethod.AddressLine1 = stripePaymentMethod.BillingDetails.Address?.Line1;
                                paymentMethod.AddressLine2 = stripePaymentMethod.BillingDetails.Address?.Line2;
                                paymentMethod.City = stripePaymentMethod.BillingDetails.Address?.City;
                                paymentMethod.State = stripePaymentMethod.BillingDetails.Address?.State;
                                paymentMethod.PostalCode = stripePaymentMethod.BillingDetails.Address?.PostalCode;
                                paymentMethod.Country = stripePaymentMethod.BillingDetails.Address?.Country;
                            }

                            if (paymentMethod.Id == 0)
                                _db.PaymentMethods.Add(paymentMethod);

                            await _db.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                        catch (Exception ex)
                        {
                            await transaction.RollbackAsync();
                            orderHeader.OrderStatus = "Failed";
                            orderHeader.PaymentStatus = "Failed";
                            await _db.SaveChangesAsync();
                            TempData["Error"] = "Order failed due to insufficient stock. Please try again.";
                            return RedirectToAction("Index");
                        }
                    }
                }
                catch (Exception)
                {
                    TempData["Error"] = "An error occurred while processing your payment.";
                    return RedirectToAction("Index");
                }
            }
            else if (user.PaymentMehtod == "cash")
            {
                orderHeader.PaymentStatus = SD.PaymentStatusPending;
                orderHeader.OrderStatus = SD.StatusPending;
                await _db.SaveChangesAsync();
            }

            var cartItems = await _db.CartItems
                .Where(ci => ci.UserId == user.Id)
                .ToListAsync();

            if (cartItems.Any())
            {
                _db.CartItems.RemoveRange(cartItems);
                await _db.SaveChangesAsync();
            }

            return View(new OrderVM
            {
                OrderHeader = orderHeader,
                OrderDetails = await _db.OrderDetails
                    .Include(od => od.Product)
                    .Where(od => od.OrderHeaderId == id)
                    .ToListAsync()
            });
        }

        // Calculates order totals including subtotal, shipping, tax, and total
        private (double Subtotal, double ShippingFee, double Tax, double Total) CalculateOrderTotals(IEnumerable<CartItem> cartItems)
        {
            double subtotal = (double)cartItems.Sum(i => i.Product.Product_Price * i.Quantity);
            double shippingFee = subtotal > 100 ? 0 : 10;
            double tax = subtotal * 0.08;
            double total = subtotal + shippingFee + tax;

            return (subtotal, shippingFee, tax, total);
        }
    }
}