using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class ProfileController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }
        public async Task<IActionResult> Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value; // Get the current user's ID

            var profile = await _db.ApplicationUsers.Where(u => u.Id == userId).FirstOrDefaultAsync();

            if (profile == null)
            {
                return RedirectToAction("Edit");
            }
            var model = new ProfileViewModel
            {
                Id = profile.Id,
                Name = profile.Name,
                Email = profile.Email,
                PhoneNumber = profile.PhoneNumber,
                Address = profile.StreetAddress,
                PostalCode = profile.PostalCode,
                Country = profile.Country,
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateName(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.Name = model.Name;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Last Name updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEmail(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }
            if (user.Email != model.Email)
            {
                user.Email = model.Email;
                user.EmailConfirmed = false;
            }

            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Email updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePhone(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }
            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Phone Number updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAddress(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.StreetAddress = model.Address;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Address updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePostalCode(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.PostalCode = model.PostalCode;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Postal Code updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCountry(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.Country = model.Country;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "City updated successfully!";
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Security()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                Email = user.Email,
                IsEmailConfirmed = user.EmailConfirmed,
                IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
                ConnectedDevices = await _db.UserDevices
                    .Where(d => d.UserId == user.Id)
                    .OrderByDescending(d => d.LastLoginDate)
                    .Take(2)
                    .ToListAsync(),
                RecentSecurityActivities = await _db.SecurityActivities
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .ToListAsync()
            };
            return View(model);
        }
    }
}
