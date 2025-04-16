using System.Diagnostics;
using Clothes_Store.Models;
using Clothes_Store.Services;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clothes_Store.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Admin)]

    public class AnalyticsController : Controller
    {
        // Controllers/AnalyticsController.cs
        private readonly IUserAnalyticsService _userAnalyticsService;

        public AnalyticsController(IUserAnalyticsService userAnalyticsService)
        {
            _userAnalyticsService = userAnalyticsService;
        }
        public async Task<IActionResult> Index()
        {
            return View();
        }
        public async Task<IActionResult> UserDashboard(int? days)
        {
            // Default to 30 days if not specified
            var daysToShow = days ?? 30;

            var model = await _userAnalyticsService.GetUserAnalytics(daysToShow);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserGrowthData(int days)
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-days);

            var data = await _userAnalyticsService.GetUserGrowthData(startDate, endDate, days);

            return Json(data);
        }
    }
}
