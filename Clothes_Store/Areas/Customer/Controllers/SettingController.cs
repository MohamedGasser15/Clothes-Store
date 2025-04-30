using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class SettingController : BaseController
    {
        // Constructor initializes database context and user manager
        public SettingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
            : base(db, userManager)
        {
        }

        // Displays user settings page
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            return View(user);
        }

        // Changes user's preferred language and updates session
        [HttpPost]
        public async Task<IActionResult> ChangeLanguage(string PreferredLanguage)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                user.PreferredLanguage = PreferredLanguage;
                await _userManager.UpdateAsync(user);
                HttpContext.Session?.SetString("lang", PreferredLanguage);

                TempData["SuccessMessage"] = "Language changed successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Failed to change language";
                return RedirectToAction(nameof(Index));
            }
        }

        // Changes user's preferred currency
        [HttpPost]
        public async Task<IActionResult> ChangeCurrency(string Currency)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                user.Currency = Currency;
                await _userManager.UpdateAsync(user);

                TempData["SuccessMessage"] = "Currency changed successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Failed to change currency";
                return RedirectToAction(nameof(Index));
            }
        }

        // Updates user's preferred payment method
        [HttpPost]
        public async Task<IActionResult> ChangePayment(string PaymentMethod)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                user.PaymentMehtod = PaymentMethod; // Note: Typo in property name (Mehtod)
                await _userManager.UpdateAsync(user);

                TempData["SuccessMessage"] = "Payment method updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Failed to update payment method";
                return RedirectToAction(nameof(Index));
            }
        }

        // Updates user's preferred shipping carriers
        [HttpPost]
        public async Task<IActionResult> ChangePreferredCarriers(string PreferredCarriers)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                user.PreferredCarriers = PreferredCarriers;
                await _userManager.UpdateAsync(user);

                TempData["SuccessMessage"] = "Preferred carriers updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "Failed to update preferred carriers";
                return RedirectToAction(nameof(Index));
            }
        }

        // Updates user's primary address
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePrimaryAddress(string userId, string addressValue)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                user.StreetAddress = addressValue;
                user.SelectedAddress = "primary";
                var result = await _userManager.UpdateAsync(user);

                TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                    result.Succeeded ? "Primary address updated successfully" : "Failed to update primary address";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating address: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // Updates user's secondary address
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSecondaryAddress(string userId, string addressValue)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                user.StreetAddress2 = addressValue;
                var result = await _userManager.UpdateAsync(user);

                TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                    result.Succeeded ? "Secondary address updated successfully" : "Failed to update secondary address";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating address: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // Swaps primary and secondary addresses
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetPrimaryAddress(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (!string.IsNullOrEmpty(user.StreetAddress2))
                {
                    var temp = user.StreetAddress;
                    user.StreetAddress = user.StreetAddress2;
                    user.StreetAddress2 = temp;
                    user.SelectedAddress = "primary";

                    var result = await _userManager.UpdateAsync(user);

                    TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                        result.Succeeded ? "Primary address changed successfully" : "Failed to update primary address";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating address: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // Deletes user's secondary address
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSecondaryAddress(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                user.StreetAddress2 = null;
                if (user.SelectedAddress == "secondary")
                    user.SelectedAddress = "primary";

                var result = await _userManager.UpdateAsync(user);

                TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                    result.Succeeded ? "Secondary address removed" : "Failed to remove address";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error removing address: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // Displays shipping settings page
        public IActionResult Shipping()
        {
            return View();
        }

        // Displays notifications settings page
        public IActionResult Notifications()
        {
            return View();
        }

        // Displays privacy settings page
        public IActionResult Privacy()
        {
            return View();
        }
    }
}