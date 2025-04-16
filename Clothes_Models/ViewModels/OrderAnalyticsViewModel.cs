using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    // OrderAnalyticsViewModels.cs
    public class OrderAnalyticsViewModel
    {
        // Main dashboard container
        public RevenueTrendViewModel RevenueTrend { get; set; } = new();
        public OrderVolumeViewModel OrderVolume { get; set; } = new();
        public OrderStatusDistributionViewModel OrderStatusDistribution { get; set; } = new();
        public PaymentStatusDistributionViewModel PaymentStatusDistribution { get; set; } = new();
        public TopProductsViewModel TopProducts { get; set; } = new();
        public SalesByDayOfWeekViewModel SalesByDayOfWeek { get; set; } = new();
        public SalesByCategoryViewModel SalesByCategory { get; set; } = new();
        public SalesByHourViewModel SalesByHour { get; set; } = new();
        public KeyMetricsViewModel KeyMetrics { get; set; } = new();
        public int TotalDays { get; set; } = 30;
    }

    public class RevenueTrendViewModel
    {
        public List<string> Labels { get; set; } = new();
        public List<decimal> Revenue { get; set; } = new();
        public List<decimal> Subtotal { get; set; } = new();
        public List<decimal> ShippingFees { get; set; } = new();
        public List<decimal> Taxes { get; set; } = new();
        public List<int> OrderCounts { get; set; } = new();
    }

    public class OrderVolumeViewModel
    {
        public List<string> Labels { get; set; } = new();
        public List<int> CompletedOrders { get; set; } = new();
        public List<int> PendingOrders { get; set; } = new();
        public List<int> CancelledOrders { get; set; } = new();
    }

    public class OrderStatusDistributionViewModel
    {
        public List<string> Statuses { get; set; } = new();
        public List<int> Counts { get; set; } = new();
        public List<string> Colors { get; set; } = new()
    {
    "#10B981", // Delivered - Emerald (success)
    "#F59E0B", // Pending - Blue (awaiting action)
    "#3B82F6", // Processing - Amber (in progress)
    "#EF4444", // Cancelled - Red (error/failure)
    "#8B5CF6", // Refunded - Purple (money returned)
    "#06B6D4", // Shipped - Cyan (in transit)
    "#84CC16", // Approved - Lime (verified)
    };
        public List<decimal> RevenueByStatus { get; set; } = new();
    }

    public class PaymentStatusDistributionViewModel
    {
        public List<string> Statuses { get; set; } = new();
        public List<int> Counts { get; set; } = new();
        public List<string> Colors { get; set; } = new()
    {
        "#10B981", // Paid
        "#3B82F6", // Pending
        "#EF4444"  // Refunded
    };
    }

    public class TopProductsViewModel
    {
        public List<string> ProductNames { get; set; } = new();
        public List<int> QuantitiesSold { get; set; } = new();
        public List<decimal> RevenueGenerated { get; set; } = new();
        public List<string> ProductImages { get; set; } = new();
    }

    public class SalesByCategoryViewModel
    {
        public List<string> CategoryNames { get; set; } = new();
        public List<int> OrderCounts { get; set; } = new();
        public List<decimal> Revenue { get; set; } = new();
        public List<string> Colors { get; set; } = new()
    {
        "#6366F1", "#10B981", "#3B82F6",
        "#F59E0B", "#EC4899", "#F97316"
    };
    }

    public class SalesByDayOfWeekViewModel
    {
        public List<string> Days { get; set; } = new()
    {
        "Sunday", "Monday", "Tuesday", "Wednesday",
        "Thursday", "Friday", "Saturday"
    };
        public List<decimal> Revenue { get; set; } = new();
        public List<int> OrderCounts { get; set; } = new();
    }

    public class SalesByHourViewModel
    {
        public List<string> Hours { get; set; } = new()
    {
        "00:00", "01:00", "02:00", "03:00", "04:00", "05:00",
        "06:00", "07:00", "08:00", "09:00", "10:00", "11:00",
        "12:00", "13:00", "14:00", "15:00", "16:00", "17:00",
        "18:00", "19:00", "20:00", "21:00", "22:00", "23:00"
    };
        public List<decimal> Revenue { get; set; } = new();
        public List<int> OrderCounts { get; set; } = new();
    }

    public class KeyMetricsViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal RevenueChangePercentage { get; set; }
        public int TotalOrders { get; set; }
        public int OrderChangePercentage { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal AOVChangePercentage { get; set; }
        public int NewCustomers { get; set; }
        public decimal RefundRate { get; set; }
        public Dictionary<string, decimal> ConversionRates { get; set; } = new()
        {
            ["CartToCheckout"] = 0m,
            ["CheckoutToPurchase"] = 0m
        };
    }

    public class OrderAnalyticsFilter
    {
        public int Days { get; set; } = 30;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? OrderStatus { get; set; }
        public string? PaymentStatus { get; set; }
        public int? CategoryId { get; set; }
        public int? ProductId { get; set; }
        public string? Country { get; set; }
        public bool CompareWithPreviousPeriod { get; set; } = false;
    }
}
