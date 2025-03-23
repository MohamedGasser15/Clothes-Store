using Clothes_DataAccess.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ProfileController(ApplicationDbContext db)
        {
            _db = db;
        }
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.Id == userId);

            if (profile == null)
            {
                return RedirectToAction("Edit");
            }

            return View(profile);
        }
    }
}
