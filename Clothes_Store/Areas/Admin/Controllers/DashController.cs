using System.Diagnostics;
using Clothes_Store.Models;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Controllers
{
    [Area("Admin")]

    public class DashController : Controller
    {


        public IActionResult Index()
        {
            return View();
        }

    }
}
