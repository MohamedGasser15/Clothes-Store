﻿using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        public UserController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        // Displays the list of all users with their roles
        public IActionResult Index()
        {
            var UserAccount = _db.ApplicationUsers.ToList();
            var userRole = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList();
            foreach (var user in UserAccount)
            {
                var role = userRole.FirstOrDefault(u => u.UserId == user.Id);
                user.Role = role == null ? "None" : roles.FirstOrDefault(u => u.Id == role.RoleId).Name;
            }
            return View(UserAccount);
        }

        // Displays the form for editing a user's details
        public IActionResult Edit(string userId)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userId);
            if (objFromDb == null)
                return NotFound();

            var userRole = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList();
            var role = userRole.FirstOrDefault(u => u.UserId == objFromDb.Id);
            if (role != null)
                objFromDb.RoleId = roles.FirstOrDefault(u => u.Id == role.RoleId).Id;

            objFromDb.RoleList = _db.Roles.Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Text = u.Name,
                Value = u.Id
            });
            return View(objFromDb);
        }

        // Updates a user's details and role
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ApplicationUser user)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == user.Id);
            if (objFromDb == null)
            {
                TempData["Error"] = "Oops! Something went wrong. Please try again.";
                return NotFound();
            }

            var userRole = _db.UserRoles.FirstOrDefault(u => u.UserId == objFromDb.Id);
            if (userRole != null)
            {
                var previousRoleName = _db.Roles.Where(u => u.Id == userRole.RoleId).Select(e => e.Name).FirstOrDefault();
                await _userManager.RemoveFromRoleAsync(objFromDb, previousRoleName);
            }

            await _userManager.AddToRoleAsync(objFromDb, _db.Roles.FirstOrDefault(u => u.Id == user.RoleId).Name);
            objFromDb.Name = user.Name;
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var activity = new AdminActivity
            {
                UserId = _userManager.GetUserId(User),
                ActivityType = "EditUser",
                Description = $"Edit User (Email: '{objFromDb.Email}')",
                IpAddress = ipAddress
            };

            _db.AdminActivities.Add(activity);
            _db.SaveChanges();

            TempData["Success"] = $"User ('{objFromDb.Name}') updated successfully";
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // Deletes a user and their associated devices
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string userId)
        {
            try
            {
                var obj = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userId);
                if (obj == null)
                {
                    TempData["Error"] = "User not found.";
                    return RedirectToAction(nameof(Index));
                }

                var userDevices = _db.UserDevices.Where(ud => ud.UserId == userId);
                _db.UserDevices.RemoveRange(userDevices);
                _db.ApplicationUsers.Remove(obj);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                var activity = new AdminActivity
                {
                    UserId = _userManager.GetUserId(User),
                    ActivityType = "DeleteUser",
                    Description = $"Delete User (Email: '{obj.Email}')",
                    IpAddress = ipAddress
                };

                _db.AdminActivities.Add(activity);
                _db.SaveChanges();

                TempData["Success"] = $"User '{obj.Name}' deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["Error"] = "An error occurred while deleting the user. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // Locks or unlocks a user account
        public IActionResult LockUnlock(string userId)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userId);
            if (objFromDb == null)
            {
                TempData["Error"] = "User not found! The account may have been deleted or doesn't exist.";
                return NotFound();
            }

            if (objFromDb.LockoutEnd != null && objFromDb.LockoutEnd > DateTime.Now)
            {
                objFromDb.LockoutEnd = DateTime.Now;
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var activity = new AdminActivity
                {
                    UserId = _userManager.GetUserId(User),
                    ActivityType = "UnlockUser",
                    Description = $"Unlock User (Email: '{objFromDb.Email}')",
                    IpAddress = ipAddress
                };
                _db.AdminActivities.Add(activity);

                TempData["Success"] = $"Successfully unlocked user ({objFromDb.Name})!";
            }
            else
            {
                objFromDb.LockoutEnd = DateTime.Now.AddDays(10);
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                var activity = new AdminActivity
                {
                    UserId = _userManager.GetUserId(User),
                    ActivityType = "LockUser",
                    Description = $"Lock User (Email: '{objFromDb.Email}')",
                    IpAddress = ipAddress
                };
                _db.AdminActivities.Add(activity);

                TempData["Success"] = $"Successfully locked user ({objFromDb.Name}) for 10 days!";
            }

            _db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
    }
}