using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Clothes_Store.Services
{
    public interface IOrderAnalyticsService
    {
        Task<OrderAnalyticsViewModel> GetDashboardData(OrderAnalyticsFilter filter);
        Task<RevenueTrendViewModel> GetRevenueTrendData(DateTime startDate, DateTime endDate, int days);

        // Add other method signatures as needed
        Task<OrderStatusDistributionViewModel> GetOrderStatusData(DateTime startDate, DateTime endDate);
        Task<TopProductsViewModel> GetTopProductsData(int topCount, DateTime startDate, DateTime endDate);
    }
    public class OrderAnalyticsService : IOrderAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderAnalyticsService> _logger;

        public OrderAnalyticsService(ApplicationDbContext context, ILogger<OrderAnalyticsService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<OrderAnalyticsViewModel> GetDashboardData(OrderAnalyticsFilter filter)
        {
            try
            {
                var endDate = filter.EndDate ?? DateTime.UtcNow;
                var startDate = filter.StartDate ?? endDate.AddDays(-filter.Days);

                return new OrderAnalyticsViewModel
                {
                    RevenueTrend = await GetRevenueTrend(startDate, endDate, filter),
                    OrderStatusDistribution = await GetOrderStatusDistribution(startDate, endDate, filter),
                    PaymentStatusDistribution = await GetPaymentStatusDistribution(startDate, endDate, filter),
                    TopProducts = await GetTopProducts(startDate, endDate, 5, filter),
                    SalesByDayOfWeek = await GetSalesByDayOfWeek(startDate, endDate, filter),
                    SalesByCategory = await GetSalesByCategory(startDate, endDate, filter),
                    SalesByHour = await GetSalesByHour(startDate, endDate, filter),
                    KeyMetrics = await GetKeyMetrics(startDate, endDate, filter),
                    TotalDays = (int)(endDate - startDate).TotalDays
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating order analytics");
                throw;
            }
        }

        private async Task<RevenueTrendViewModel> GetRevenueTrend(DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            var interval = DetermineInterval(startDate, endDate);
            var query = BuildBaseQuery(startDate, endDate, filter);

            var model = new RevenueTrendViewModel();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var (nextDate, label) = GetNextInterval(currentDate, interval);

                var ordersInPeriod = await query
                    .Where(o => o.OrderDate >= currentDate && o.OrderDate < nextDate)
                    .ToListAsync();

                model.Labels.Add(label);
                model.Revenue.Add((decimal)ordersInPeriod.Sum(o => o.OrderTotal));
                model.Subtotal.Add((decimal)ordersInPeriod.Sum(o => o.Subtotal));
                model.ShippingFees.Add((decimal)ordersInPeriod.Sum(o => o.ShippingFee));
                model.Taxes.Add((decimal)ordersInPeriod.Sum(o => o.Tax));
                model.OrderCounts.Add(ordersInPeriod.Count);

                currentDate = nextDate;
            }

            return model;
        }

        private async Task<OrderStatusDistributionViewModel> GetOrderStatusDistribution(
     DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            var query = BuildBaseQuery(startDate, endDate, filter);

            var statusGroups = await query
                .GroupBy(o => o.OrderStatus ?? "Unknown")
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count(),
                    Revenue = g.Sum(o => o.OrderTotal)
                })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            var model = new OrderStatusDistributionViewModel();

            // Ensure we have all standard statuses, even if count is 0
            var standardStatuses = new List<string> { "Delivered", "Pending", "Processing", "Cancelled", "Refunded", "Shipped", "Approved" };

            foreach (var status in standardStatuses)
            {
                var group = statusGroups.FirstOrDefault(g => string.Equals(g.Status, status, StringComparison.OrdinalIgnoreCase));
                if (group != null)
                {
                    model.Statuses.Add(status);
                    model.Counts.Add(group.Count);
                    model.RevenueByStatus.Add((decimal)group.Revenue);
                }
                else
                {
                    model.Statuses.Add(status);
                    model.Counts.Add(0);
                    model.RevenueByStatus.Add(0);
                }
            }

            // Add any non-standard statuses
            foreach (var group in statusGroups.Where(g => !standardStatuses.Contains(g.Status, StringComparer.OrdinalIgnoreCase)))
            {
                model.Statuses.Add(group.Status);
                model.Counts.Add(group.Count);
                model.RevenueByStatus.Add((decimal)group.Revenue);
            }

            // Only keep as many colors as we have statuses
            model.Colors = model.Colors.Take(model.Statuses.Count).ToList();

            return model;
        }

        private async Task<TopProductsViewModel> GetTopProducts(
            DateTime startDate, DateTime endDate, int topCount, OrderAnalyticsFilter filter)
        {
            var query = _context.OrderDetails
                .Include(od => od.OrderHeader)
                .Include(od => od.Product)
                .Where(od => od.OrderHeader.OrderDate >= startDate && od.OrderHeader.OrderDate <= endDate);

            if (filter.ProductId.HasValue)
                query = query.Where(od => od.ProductId == filter.ProductId.Value);

            var productGroups = await query
                .GroupBy(od => new { od.ProductId, od.Product.Product_Name, od.Product.imgUrl })
                .Select(g => new
                {
                    g.Key.Product_Name,
                    g.Key.imgUrl,
                    Quantity = g.Sum(od => od.Count),
                    Revenue = g.Sum(od => od.price * od.Count)
                })
                .OrderByDescending(g => g.Revenue)
                .Take(topCount)
                .ToListAsync();

            var model = new TopProductsViewModel();

            foreach (var product in productGroups)
            {
                model.ProductNames.Add(product.Product_Name);
                model.QuantitiesSold.Add(product.Quantity);
                model.RevenueGenerated.Add((decimal)product.Revenue);
                model.ProductImages.Add(product.imgUrl ?? "/images/default-product.png");
            }

            return model;
        }

        private async Task<KeyMetricsViewModel> GetKeyMetrics(
            DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            var query = BuildBaseQuery(startDate, endDate, filter);
            var previousPeriodQuery = BuildBaseQuery(
                startDate.AddDays(-(endDate - startDate).TotalDays),
                startDate,
                filter);

            var currentOrders = await query.ToListAsync();
            var previousOrders = await previousPeriodQuery.ToListAsync();

            var currentRevenue = currentOrders.Sum(o => o.OrderTotal);
            var previousRevenue = previousOrders.Sum(o => o.OrderTotal);
            var revenueChange = previousRevenue > 0
                ? (currentRevenue - previousRevenue) / previousRevenue
                : 0;

            return new KeyMetricsViewModel
            {
                TotalRevenue = (decimal)currentRevenue,
                RevenueChangePercentage = (decimal)revenueChange,
                TotalOrders = currentOrders.Count,
                OrderChangePercentage = (int)(previousOrders.Count > 0
                    ? (currentOrders.Count - previousOrders.Count) / (double)previousOrders.Count
                    : 0),
                AverageOrderValue = currentOrders.Count > 0
                    ? (decimal)(currentRevenue / currentOrders.Count)
                    : 0,
                NewCustomers = await GetNewCustomerCount(startDate, endDate, filter)
            };
        }

        private IQueryable<OrderHeader> BuildBaseQuery(DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            var query = _context.OrderHeaders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate);

            if (!string.IsNullOrEmpty(filter.OrderStatus))
                query = query.Where(o => o.OrderStatus == filter.OrderStatus);

            if (!string.IsNullOrEmpty(filter.PaymentStatus))
                query = query.Where(o => o.PaymentStatus == filter.PaymentStatus);

            if (!string.IsNullOrEmpty(filter.Country))
                query = query.Where(o => o.Country == filter.Country);

            return query;
        }

        private async Task<int> GetNewCustomerCount(DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            // Get all users who placed their first order in this period
            var query = _context.OrderHeaders
                .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                .GroupBy(o => o.ApplicationUserId)
                .Where(g => g.Min(o => o.OrderDate) >= startDate); // First order is in this period

            if (!string.IsNullOrEmpty(filter.Country))
                query = query.Where(g => g.First().Country == filter.Country);

            return await query.CountAsync();
        }

        private string DetermineInterval(DateTime startDate, DateTime endDate)
        {
            var days = (endDate - startDate).TotalDays;
            return days <= 7 ? "day" : days <= 30 ? "week" : "month";
        }

        private (DateTime nextDate, string label) GetNextInterval(DateTime currentDate, string interval)
        {
            return interval switch
            {
                "day" => (currentDate.AddDays(1), currentDate.ToString("MMM dd")),
                "week" => (currentDate.AddDays(7), $"Week {GetWeekOfMonth(currentDate)}"),
                _ => (currentDate.AddMonths(1), currentDate.ToString("MMM yyyy"))
            };
        }

        private int GetWeekOfMonth(DateTime date)
        {
            date = date.Date;
            DateTime firstDayOfMonth = new(date.Year, date.Month, 1);
            DateTime firstMonday = firstDayOfMonth.AddDays((DayOfWeek.Monday + 7 - firstDayOfMonth.DayOfWeek) % 7);
            if (firstMonday > date)
            {
                firstDayOfMonth = firstDayOfMonth.AddMonths(-1);
                firstMonday = firstDayOfMonth.AddDays((DayOfWeek.Monday + 7 - firstDayOfMonth.DayOfWeek) % 7);
            }
            return (date - firstMonday).Days / 7 + 1;
        }

        private async Task<PaymentStatusDistributionViewModel> GetPaymentStatusDistribution(
            DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            var query = BuildBaseQuery(startDate, endDate, filter);

            var statusGroups = await query
                .GroupBy(o => o.PaymentStatus ?? "Unknown")
                .Select(g => new
                {
                    Status = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            var model = new PaymentStatusDistributionViewModel();

            foreach (var group in statusGroups)
            {
                model.Statuses.Add(group.Status);
                model.Counts.Add(group.Count);
            }

            // Only keep as many colors as we have statuses
            model.Colors = model.Colors.Take(model.Statuses.Count).ToList();

            return model;
        }

        private async Task<SalesByDayOfWeekViewModel> GetSalesByDayOfWeek(
            DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            // First get the raw data from the database
            var orders = await BuildBaseQuery(startDate, endDate, filter)
                .Select(o => new { o.OrderDate, o.OrderTotal })
                .ToListAsync();

            // Then perform the grouping in memory
            var dayGroups = orders
                .GroupBy(o => o.OrderDate.DayOfWeek)
                .Select(g => new
                {
                    DayOfWeek = g.Key,
                    Revenue = g.Sum(o => o.OrderTotal),
                    OrderCount = g.Count()
                })
                .ToList();

            var model = new SalesByDayOfWeekViewModel();

            // Initialize all days with 0 values
            for (int i = 0; i < 7; i++)
            {
                model.Revenue.Add(0);
                model.OrderCounts.Add(0);
            }

            // Fill in actual values
            foreach (var group in dayGroups)
            {
                int dayIndex = (int)group.DayOfWeek;
                model.Revenue[dayIndex] = (decimal)group.Revenue;
                model.OrderCounts[dayIndex] = group.OrderCount;
            }

            return model;
        }

        private async Task<SalesByCategoryViewModel> GetSalesByCategory(
                DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            var query = _context.OrderDetails
                .Include(od => od.OrderHeader)
                .Include(od => od.Product)
                .ThenInclude(p => p.Category)
                .Where(od => od.OrderHeader.OrderDate >= startDate &&
                            od.OrderHeader.OrderDate <= endDate);

            if (filter.CategoryId.HasValue)
                query = query.Where(od => od.Product.Category_Id == filter.CategoryId.Value);

            var categoryGroups = await query
                .GroupBy(od => new { od.Product.Category.Category_Id, od.Product.Category.Category_Name })
                .Select(g => new
                {
                    g.Key.Category_Name,
                    Revenue = g.Sum(od => od.price * od.Count),
                    OrderCount = g.Select(od => od.OrderHeaderId).Distinct().Count()
                })
                .OrderByDescending(g => g.Revenue)
                .ToListAsync();

            var model = new SalesByCategoryViewModel();

            foreach (var group in categoryGroups)
            {
                model.CategoryNames.Add(group.Category_Name);
                model.Revenue.Add((decimal)group.Revenue);
                model.OrderCounts.Add(group.OrderCount);
            }

            // Only keep as many colors as we have categories
            model.Colors = model.Colors.Take(model.CategoryNames.Count).ToList();

            return model;
        }

        private async Task<SalesByHourViewModel> GetSalesByHour(
            DateTime startDate, DateTime endDate, OrderAnalyticsFilter filter)
        {
            var query = BuildBaseQuery(startDate, endDate, filter);

            var hourGroups = await query
                .GroupBy(o => o.OrderDate.Hour)
                .Select(g => new
                {
                    Hour = g.Key,
                    Revenue = g.Sum(o => o.OrderTotal),
                    OrderCount = g.Count()
                })
                .OrderBy(g => g.Hour)
                .ToListAsync();

            var model = new SalesByHourViewModel();

            // Initialize all hours with 0 values
            for (int i = 0; i < 24; i++)
            {
                model.Revenue.Add(0);
                model.OrderCounts.Add(0);
            }

            // Fill in actual values
            foreach (var group in hourGroups)
            {
                model.Revenue[group.Hour] = (decimal)group.Revenue;
                model.OrderCounts[group.Hour] = group.OrderCount;
            }

            return model;
        }
        public async Task<RevenueTrendViewModel> GetRevenueTrendData(DateTime startDate, DateTime endDate, int days)
        {
            return await GetRevenueTrend(startDate, endDate, new OrderAnalyticsFilter
            {
                Days = days,
                StartDate = startDate,
                EndDate = endDate
            });
        }

        public async Task<OrderStatusDistributionViewModel> GetOrderStatusData(DateTime startDate, DateTime endDate)
        {
            return await GetOrderStatusDistribution(startDate, endDate, new OrderAnalyticsFilter());
        }

        public async Task<TopProductsViewModel> GetTopProductsData(int topCount, DateTime startDate, DateTime endDate)
        {
            return await GetTopProducts(startDate, endDate, topCount, new OrderAnalyticsFilter());
        }
    }
}

