using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    public class ProductAnalyticsViewModel
    {
        public KeyMetrics KeyMetrics { get; set; }
        public TopProductsData TopProducts { get; set; }
        public InventoryStatusData InventoryStatus { get; set; }
        public SalesByCategoryData SalesByCategory { get; set; }
        public List<LowStockItem> LowStockItems { get; set; }
    }

    public class KeyMetrics
    {
        public int TotalProducts { get; set; }
        public string TopSellingProduct { get; set; }
        public int TopProductSales { get; set; }
        public decimal InventoryValue { get; set; }
        public decimal InventoryChange { get; set; }
        public int LowStockItems { get; set; }
    }

    public class TopProductsData
    {
        public List<string> ProductNames { get; set; }
        public List<int> SalesCounts { get; set; }
        public List<decimal> RevenueGenerated { get; set; }
        public int TotalSales { get; set; }
    }

    public class InventoryStatusData
    {
        public List<string> Statuses { get; set; }
        public List<int> Counts { get; set; }
        public List<string> Colors { get; set; }
        public int TotalItems { get; set; }
    }

    public class SalesByCategoryData
    {
        public List<string> CategoryNames { get; set; }
        public List<int> SalesCounts { get; set; }
        public List<decimal> Revenue { get; set; }
        public List<string> Colors { get; set; }
        public int TotalSales { get; set; }
    }

    public class LowStockItem
    {
        public string ProductName { get; set; }
        public string Category { get; set; }
        public string Size { get; set; }
        public int CurrentStock { get; set; }
        public int Threshold { get; set; }
        public DateTime? LastSoldDate { get; set; }
    }
}
