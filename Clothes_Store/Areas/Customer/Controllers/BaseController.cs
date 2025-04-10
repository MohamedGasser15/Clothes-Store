using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]

    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _db;
        protected readonly UserManager<ApplicationUser> _userManager;

        public BaseController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }
    }
}
