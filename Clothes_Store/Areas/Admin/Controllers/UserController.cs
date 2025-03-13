using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UserController : Controller
    {

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public UserController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _userManager = userManager;
            _db = db;
        }

        public IActionResult Index()
        {
            var UserAccount = _db.ApplicationUsers.ToList();
            var userRole = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList();
            foreach (var user in UserAccount)
            {
                var role = userRole.FirstOrDefault(u => u.UserId == user.Id);
                if (role == null)
                {
                    user.Role = "None";
                }
                else
                {
                    user.Role = roles.FirstOrDefault(u => u.Id == role.RoleId).Name;
                }
            }
            return View(UserAccount);
        }

        public IActionResult Edit(string userId)
        {
            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userId);
            if (objFromDb == null)
            {
                return NotFound();
            }
            var userRole = _db.UserRoles.ToList();
            var roles = _db.Roles.ToList();
            var role = userRole.FirstOrDefault(u => u.UserId == objFromDb.Id);
            if (role != null)
            {
                objFromDb.RoleId = roles.FirstOrDefault(u => u.Id == role.RoleId).Id;

            }
            objFromDb.RoleList = _db.Roles.Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Text = u.Name,
                Value = u.Id
            });
            return View(objFromDb);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ApplicationUser user)
        {

            var objFromDb = _db.ApplicationUsers.FirstOrDefault(u => u.Id == user.Id);
            if (objFromDb == null)
            {
                return NotFound();
            }
            var userRole = _db.UserRoles.FirstOrDefault(u => u.UserId == objFromDb.Id);
            if (userRole != null)
            {
                var previousRoleName = _db.Roles.Where(u => u.Id == userRole.RoleId).Select(e => e.Name).FirstOrDefault();
                //removing the old role
                await _userManager.RemoveFromRoleAsync(objFromDb, previousRoleName);

            }

            //add new role
            await _userManager.AddToRoleAsync(objFromDb, _db.Roles.FirstOrDefault(u => u.Id == user.RoleId).Name);
            objFromDb.Name = user.Name;
            _db.SaveChanges();
            return RedirectToAction(nameof(Index));



            user.RoleList = _db.Roles.Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Text = u.Name,
                Value = u.Id
            });
            return View(user);
        }

        public IActionResult Delete(string userId)
        {
            ApplicationUser obj = new();
            obj = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userId);
            if (obj == null)
            {
                return NotFound();
            }
            _db.ApplicationUsers.Remove(obj);
            _db.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

    }
}
