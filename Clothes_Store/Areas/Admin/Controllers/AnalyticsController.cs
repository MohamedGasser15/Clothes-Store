using System.Diagnostics;
using Clothes_Models.ViewModels;
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
        private readonly IOrderAnalyticsService _orderAnalytics;
        private readonly IProductAnalyticsService _productAnalyticsService;
        public AnalyticsController(IUserAnalyticsService userAnalyticsService, IOrderAnalyticsService orderAnalytics, IProductAnalyticsService productAnalyticsService)
        {
            _userAnalyticsService = userAnalyticsService;
            _orderAnalytics = orderAnalytics;
            _productAnalyticsService = productAnalyticsService;
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

        [HttpGet("orders")]
        public async Task<IActionResult> OrderDashboard([FromQuery] int days = 30)
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-days);

            var filter = new OrderAnalyticsFilter
            {
                Days = days,
                StartDate = startDate,
                EndDate = endDate
            };

            var model = await _orderAnalytics.GetDashboardData(filter);

            // Add debug information
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View(model);
        }

        [HttpGet("orders/data")]
        public async Task<IActionResult> GetOrderAnalyticsData([FromQuery] OrderAnalyticsFilter filter)
        {
            var data = await _orderAnalytics.GetDashboardData(filter);
            return Json(data);
        }

        public async Task<IActionResult> ProductDashboard(int days = 30)
        {
            var model = await _productAnalyticsService.GetProductAnalytics(days);
            return View(model);
        }
        public async Task<IActionResult> MarketingDashboard()
        {
            return View();
        }
        public async Task<IActionResult> FinancialDashboard()
        {
            return View();
        }
        public async Task<IActionResult> ReportsDashboard()
        {
            return View();
        }
    }
}
