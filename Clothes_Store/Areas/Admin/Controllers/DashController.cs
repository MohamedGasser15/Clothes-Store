using System.Diagnostics;
using Clothes_Store.Models;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]

    public class DashController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

    }
}
