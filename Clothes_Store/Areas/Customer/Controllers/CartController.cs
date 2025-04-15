using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : BaseController
    {
        public CartController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : base(db, userManager)
        {
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _db.CartItems
                .Include(ci => ci.Product)
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var product = await _db.Products.FindAsync(productId);

                if (product == null)
                {
                    return Json(new { success = false, message = "Product not found" });
                }

                var existingItem = await _db.CartItems
                    .FirstOrDefaultAsync(ci => ci.ProductId == productId && ci.UserId == user.Id);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    var newItem = new CartItem
                    {
                        ProductId = productId,
                        UserId = user.Id,
                        Quantity = quantity
                    };
                    _db.CartItems.Add(newItem);
                }

                await _db.SaveChangesAsync();

                var cartCount = await _db.CartItems
                    .Where(ci => ci.UserId == user.Id)
                    .SumAsync(ci => ci.Quantity);

                return Json(new
                {
                    success = true,
                    message = "Item added to cart successfully!",
                    cartCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

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

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> IncreaseQuantity(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItem = await _db.CartItems
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == user.Id);

            if (cartItem == null)
            {
                return NotFound();
            }

            // Check stock availability if needed
            if (cartItem.Quantity > 0)
            {
                cartItem.Quantity += 1;
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DecreaseQuantity(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItem = await _db.CartItems
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == id && ci.UserId == user.Id);

            if (cartItem == null)
            {
                return NotFound();
            }

            if (cartItem.Quantity > 1)
            {
                cartItem.Quantity -= 1;
                await _db.SaveChangesAsync();
            }
            else
            {
                _db.CartItems.Remove(cartItem);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

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
        [HttpPost]
        public async Task<IActionResult> ChangePayment(string PaymentMethod)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            try
            {
                if (user != null)
                {
                    user.PaymentMehtod = PaymentMethod;
                    await _userManager.UpdateAsync(user);
                    TempData["SuccessMessage"] = "Payment method updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "User not found";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to update payment method";
            }

            return RedirectToAction(nameof(Summary));
        }

        [HttpGet]
        public async Task<IActionResult> Summary()
        {
            var user = await _userManager.GetUserAsync(User);
            var cartItems = await _db.CartItems
                .Include(ci => ci.Product)
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
                    OrderStatus = "Pending",
                    PaymentStatus = "Pending",
                    Name = user.Name,
                    PhoneNumber = user.PhoneNumber,
                    StreetAddress = user.StreetAddress,
                    Country = user.Country,
                    PostalCode = user.PostalCode
                },
                User = user // Add this line to set the User property
            };

            return View(viewModel);
        }

        [HttpPost]
        [ActionName("Summary")]
        [Route("Customer/Cart/Summary")]
        public async Task<IActionResult> SummaryPOST(CartViewModel viewModel, string selectedPaymentMethod, bool isFinalSubmission = false)
        {
            var user = await _userManager.GetUserAsync(User);
            viewModel.User = user;

            // Handle payment method update (not final submission)
            if (!isFinalSubmission && !string.IsNullOrEmpty(selectedPaymentMethod))
            {
                // Get or create payment method
                var paymentMethod = await _db.PaymentMethods
                    .FirstOrDefaultAsync(p => p.UserId == user.Id && p.IsDefault)
                    ?? new Clothes_Models.Models.PaymentMethod
                    {
                        UserId = user.Id,
                        IsDefault = true,
                        CreatedAt = DateTime.UtcNow
                    };

                // Update payment method type
                paymentMethod.CardBrand = selectedPaymentMethod == "credit" ? "card" : null;
                paymentMethod.Last4 = selectedPaymentMethod == "credit" ? "4242" : null;

                if (paymentMethod.Id == 0)
                {
                    _db.PaymentMethods.Add(paymentMethod);
                }
                else
                {
                    _db.PaymentMethods.Update(paymentMethod);
                }

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Payment method updated successfully!";
                return RedirectToAction(nameof(Summary));
            }


            // Get cart items
            var cartItems = await _db.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.UserId == user.Id)
                .ToListAsync();

            // Calculate totals
            var (subtotal, shippingFee, tax, total) = CalculateOrderTotals(cartItems);

            // Create order header
            viewModel.OrderHeader.OrderDate = DateTime.Now;
            viewModel.OrderHeader.ApplicationUserId = user.Id;
            viewModel.OrderHeader.OrderStatus = SD.StatusPending;
            viewModel.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            viewModel.OrderHeader.OrderTotal = total;
            viewModel.OrderHeader.Subtotal = subtotal;
            viewModel.OrderHeader.ShippingFee = shippingFee;
            viewModel.OrderHeader.Tax = tax;

            // Save shipping info to PaymentMethod instead of ApplicationUser
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
            {
                _db.PaymentMethods.Add(paymentMethodForOrder);
            }

            _db.OrderHeaders.Add(viewModel.OrderHeader);
            await _db.SaveChangesAsync();

            // Create order details
            foreach (var item in cartItems)
            {
                _db.OrderDetails.Add(new OrderDetail
                {
                    ProductId = item.ProductId,
                    OrderHeaderId = viewModel.OrderHeader.Id,
                    price = (double)item.Product.Product_Price,
                    Count = item.Quantity
                });
            }
            await _db.SaveChangesAsync();
            // Use the selectedPaymentMethod parameter
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

                // Update order with Stripe IDs
                viewModel.OrderHeader.SessionId = session.Id;
                viewModel.OrderHeader.PaymentIntentId = session.PaymentIntentId;
                await _db.SaveChangesAsync();

                // Clear cart
                return Redirect(session.Url);
            }
            else
            {
                // For cash payment
                await _db.SaveChangesAsync();

                // Clear cart
                _db.CartItems.RemoveRange(cartItems);
                await _db.SaveChangesAsync();

                return RedirectToAction("OrderConfirmation", new { id = viewModel.OrderHeader.Id });
            }
        }

        public async Task<IActionResult> OrderConfirmation(int id, string session_id)
        {
            var orderHeader = await _db.OrderHeaders
                .Include(oh => oh.ApplicationUser)
                .FirstOrDefaultAsync(oh => oh.Id == id);

            if (orderHeader == null) return NotFound();

            var user = orderHeader.ApplicationUser;

            if (!string.IsNullOrEmpty(session_id))
            {
                try
                {
                    var sessionService = new SessionService();
                    var session = sessionService.Get(session_id);

                    if (session.PaymentStatus == "paid")
                    {
                        orderHeader.SessionId = session_id;
                        orderHeader.PaymentIntentId = session.PaymentIntentId;
                        orderHeader.PaymentStatus = SD.PaymentStatusApproved;
                        orderHeader.OrderStatus = SD.StatusApproved;

                        var paymentIntentService = new PaymentIntentService();
                        var paymentIntent = paymentIntentService.Get(session.PaymentIntentId);

                        // NEW: Get the payment method with expand to include card details
                        var options = new PaymentMethodGetOptions
                        {
                            Expand = new List<string> { "card" }
                        };
                        var stripePaymentMethod = new PaymentMethodService().Get(paymentIntent.PaymentMethodId, options);

                        // DEBUG: Log the payment method details

                        var paymentMethod = await _db.PaymentMethods
                            .FirstOrDefaultAsync(p => p.StripePaymentMethodId == paymentIntent.PaymentMethodId);

                        if (paymentMethod == null)
                        {
                            paymentMethod = new Clothes_Models.Models.PaymentMethod
                            {
                                UserId = user.Id,
                                StripePaymentMethodId = paymentIntent.PaymentMethodId,
                                StripeCustomerId = session.CustomerId,
                                IsDefault = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            _db.PaymentMethods.Add(paymentMethod);
                        }

                        // Ensure we have card details
                        if (stripePaymentMethod?.Card != null)
                        {
                            paymentMethod.CardBrand = stripePaymentMethod.Card.Brand;
                            paymentMethod.Last4 = stripePaymentMethod.Card.Last4;
                            paymentMethod.ExpMonth = (int)stripePaymentMethod.Card.ExpMonth;
                            paymentMethod.ExpYear = (int)stripePaymentMethod.Card.ExpYear;

                            // DEBUG: Log the saved card details
                        }

                        if (stripePaymentMethod?.BillingDetails != null)
                        {
                            paymentMethod.BillingName = stripePaymentMethod.BillingDetails.Name;
                            paymentMethod.PhoneNumber = stripePaymentMethod.BillingDetails.Phone;

                            if (stripePaymentMethod.BillingDetails.Address != null)
                            {
                                paymentMethod.AddressLine1 = stripePaymentMethod.BillingDetails.Address.Line1;
                                paymentMethod.AddressLine2 = stripePaymentMethod.BillingDetails.Address.Line2;
                                paymentMethod.City = stripePaymentMethod.BillingDetails.Address.City;
                                paymentMethod.State = stripePaymentMethod.BillingDetails.Address.State;
                                paymentMethod.PostalCode = stripePaymentMethod.BillingDetails.Address.PostalCode;
                                paymentMethod.Country = stripePaymentMethod.BillingDetails.Address.Country;
                            }
                        }

                        await _db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                }
            }
            else if (user.PaymentMehtod == "cash")
            {
                orderHeader.PaymentStatus = SD.PaymentStatusPending;
                orderHeader.OrderStatus = SD.StatusPending;
                await _db.SaveChangesAsync();
            }

            // Clear cart
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
