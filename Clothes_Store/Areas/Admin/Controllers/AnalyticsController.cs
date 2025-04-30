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
        private readonly IUserAnalyticsService _userAnalyticsService;
        private readonly IOrderAnalyticsService _orderAnalytics;
        private readonly IProductAnalyticsService _productAnalyticsService;

        public AnalyticsController(IUserAnalyticsService userAnalyticsService, IOrderAnalyticsService orderAnalytics, IProductAnalyticsService productAnalyticsService)
        {
            _userAnalyticsService = userAnalyticsService;
            _orderAnalytics = orderAnalytics;
            _productAnalyticsService = productAnalyticsService;
        }
        // Displays the main analytics dashboard
        public async Task<IActionResult> Index()
        {
            return View();
        }

        // Displays user analytics for a specified number of days
        public async Task<IActionResult> UserDashboard(int? days)
        {
            var daysToShow = days ?? 30;
            var model = await _userAnalyticsService.GetUserAnalytics(daysToShow);
            return View(model);
        }

        // Retrieves user growth data for a specified period
        [HttpGet]
        public async Task<IActionResult> GetUserGrowthData(int days)
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-days);
            var data = await _userAnalyticsService.GetUserGrowthData(startDate, endDate, days);
            return Json(data);
        }

        // Displays order analytics dashboard for a specified period
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
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;

            return View(model);
        }

        // Retrieves order analytics data based on filter
        [HttpGet("orders/data")]
        public async Task<IActionResult> GetOrderAnalyticsData([FromQuery] OrderAnalyticsFilter filter)
        {
            var data = await _orderAnalytics.GetDashboardData(filter);
            return Json(data);
        }

        // Displays product analytics for a specified period
        public async Task<IActionResult> ProductDashboard(int days = 30)
        {
            var model = await _productAnalyticsService.GetProductAnalytics(days);
            return View(model);
        }

        // Displays the marketing dashboard
        public async Task<IActionResult> MarketingDashboard()
        {
            return View();
        }

        // Displays the financial dashboard
        public async Task<IActionResult> FinancialDashboard()
        {
            return View();
        }

        // Displays the reports dashboard
        public async Task<IActionResult> ReportsDashboard()
        {
            return View();
        }
    }
}