using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
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
                }
            };

            return View(viewModel);
        }
        [HttpPost]
        [ActionName("Summary")]
        public async Task<IActionResult> SummaryPOST(CartViewModel viewModel)
        {
            var user = await _userManager.GetUserAsync(User);

            // Get cart items
            var cartItems = await _db.CartItems
                .Include(ci => ci.Product)
                .Where(ci => ci.UserId == user.Id)
                .ToListAsync();

            viewModel.Items = cartItems; // Ensure Items is populated

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

            _db.OrderHeaders.Add(viewModel.OrderHeader);
            await _db.SaveChangesAsync();

            // Create order details
            foreach (var item in cartItems)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId = item.ProductId,
                    OrderHeaderId = viewModel.OrderHeader.Id,
                    price = (double)item.Product.Product_Price,
                    Count = item.Quantity
                };
                _db.OrderDetails.Add(orderDetail);
            }
            _db.CartItems.RemoveRange(cartItems);
            await _db.SaveChangesAsync();
            // Stripe configuration
            var domain = $"{Request.Scheme}://{Request.Host}";
            var successUrl = $"{domain}/Customer/Cart/OrderConfirmation?id={viewModel.OrderHeader.Id}";
            var cancelUrl = $"{domain}/Customer/Cart/Index";

            // Verify URLs (important for debugging)
            if (string.IsNullOrWhiteSpace(domain) ||
                !Uri.TryCreate(successUrl, UriKind.Absolute, out _) ||
                !Uri.TryCreate(cancelUrl, UriKind.Absolute, out _))
            {
                throw new Exception("Invalid URL configuration");
            }

            var options = new SessionCreateOptions
            {
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                Mode = "payment",
                LineItems = new List<SessionLineItemOptions>()
            };

            // Add line items
            foreach (var item in viewModel.Items)
            {
                options.LineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Product.Product_Price * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Product_Name,
                            Images = !string.IsNullOrEmpty(item.Product.imgUrl) ?
                                   new List<string> { item.Product.imgUrl } : null
                        }
                    },
                    Quantity = item.Quantity
                });
            }

            // Add shipping if applicable
            if (viewModel.ShippingFee > 0)
            {
                options.LineItems.Add(new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(viewModel.ShippingFee * 100),
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Shipping Fee"
                        }
                    },
                    Quantity = 1
                });
            }

            var service = new SessionService();
            Session session = service.Create(options);

            // Update Stripe IDs
            viewModel.OrderHeader.SessionId = session.Id;
            viewModel.OrderHeader.PaymentIntentId = session.PaymentIntentId;
            await _db.SaveChangesAsync();

            // Clear cart


            return Redirect(session.Url);
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
