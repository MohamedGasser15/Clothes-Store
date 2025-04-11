using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class SettingController : BaseController
    {
        public SettingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : base(db, userManager)
        {
        }
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            return View(user);
        }
        [HttpPost]
        public async Task<IActionResult> ChangeLanguage(string PreferredLanguage)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            try
            {
                if (user != null)
                {
                    user.PreferredLanguage = PreferredLanguage;
                    await _userManager.UpdateAsync(user);

                    if (HttpContext.Session != null)
                    {
                        HttpContext.Session.SetString("lang", PreferredLanguage);
                    }
                }

                TempData["SuccessMessage"] = "Language changed successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to change language";
                return RedirectToAction("Index");
            }
        }
        [HttpPost]
        public async Task<IActionResult> ChangeCurrency(string Currency)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);

            try
            {
                if (user != null)
                {
                    user.Currency = Currency;
                    await _userManager.UpdateAsync(user);
                }
                TempData["SuccessMessage"] = "Currency changed successfully!";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to change currency";
                return RedirectToAction("Index");
            }
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

            return RedirectToAction("Index");
        }
        [HttpPost]
        public async Task<IActionResult> ChangePreferredCarriers(string PreferredCarriers)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.FindByIdAsync(userId);
            try
            {
                if (user != null)
                {
                    user.PreferredCarriers = PreferredCarriers;
                    await _userManager.UpdateAsync(user);
                    TempData["SuccessMessage"] = "Preferred Carriers updated successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "User not found";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Failed to update Preferred Carriers";
            }

            return RedirectToAction("Index");
        }
    }
}
