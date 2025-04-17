using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Clothes_Models.ViewModels;

namespace Clothes_Store.Services
{
    public interface IProductAnalyticsService
    {
        Task<ProductAnalyticsViewModel> GetProductAnalytics(int days);
    }
    public class ProductAnalyticsService : IProductAnalyticsService
    {
        private readonly ApplicationDbContext _db;
        private const int LowStockThreshold = 10;
        private const int OutOfStockThreshold = 1;

        public ProductAnalyticsService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<ProductAnalyticsViewModel> GetProductAnalytics(int days = 30)
        {
            var cutoffDate = DateTime.Now.AddDays(-days);

            return new ProductAnalyticsViewModel
            {
                KeyMetrics = await GetKeyMetrics(cutoffDate),
                TopProducts = await GetTopProductsData(cutoffDate),
                InventoryStatus = await GetInventoryStatusData(),
                SalesByCategory = await GetSalesByCategoryData(cutoffDate),
                LowStockItems = await GetLowStockItems()
            };
        }

        private async Task<KeyMetrics> GetKeyMetrics(DateTime cutoffDate)
        {
            var totalProducts = await _db.Products.CountAsync();

            var topProductData = await _db.OrderDetails
                .Include(od => od.OrderHeader)
                .Where(od => od.OrderHeader.OrderDate >= cutoffDate)
                .GroupBy(od => od.Product.Product_Name)
                .Select(g => new
                {
                    ProductName = g.Key,
                    SalesCount = g.Sum(od => od.Count),
                    Revenue = g.Sum(od => od.Count * od.price)
                })
                .OrderByDescending(x => x.SalesCount)
                .FirstOrDefaultAsync();

            var inventoryValue = await _db.Stocks.SumAsync(s => s.Quantity * s.Product.Product_Price);

            return new KeyMetrics
            {
                TotalProducts = totalProducts,
                TopSellingProduct = topProductData?.ProductName ?? "N/A",
                TopProductSales = topProductData?.SalesCount ?? 0,
                InventoryValue = inventoryValue,
                InventoryChange = 0, // You can implement change calculation
                LowStockItems = await _db.Stocks.CountAsync(s => s.Quantity <= LowStockThreshold)
            };
        }

        private async Task<TopProductsData> GetTopProductsData(DateTime cutoffDate, int count = 5)
        {
            var topProducts = await _db.OrderDetails
                .Include(od => od.OrderHeader)
                .Where(od => od.OrderHeader.OrderDate >= cutoffDate)
                .GroupBy(od => od.Product.Product_Name)
                .Select(g => new
                {
                    ProductName = g.Key,
                    SalesCount = g.Sum(od => od.Count),
                    Revenue = (decimal)g.Sum(od => od.Count * od.price) // Explicitly cast to decimal
                })
                .OrderByDescending(x => x.SalesCount)
                .Take(count)
                .ToListAsync();

            var totalSales = await _db.OrderDetails
                .Include(od => od.OrderHeader)
                .Where(od => od.OrderHeader.OrderDate >= cutoffDate)
                .SumAsync(od => od.Count);

            return new TopProductsData
            {
                ProductNames = topProducts.Select(x => x.ProductName).ToList(),
                SalesCounts = topProducts.Select(x => x.SalesCount).ToList(),
                RevenueGenerated = topProducts.Select(x => (decimal)x.Revenue).ToList(), // Ensure type matches
                TotalSales = totalSales
            };
        }

        private async Task<InventoryStatusData> GetInventoryStatusData()
        {
            var inStock = await _db.Stocks.CountAsync(s => s.Quantity > LowStockThreshold);
            var lowStock = await _db.Stocks.CountAsync(s => s.Quantity <= LowStockThreshold && s.Quantity >= OutOfStockThreshold);
            var outOfStock = await _db.Stocks.CountAsync(s => s.Quantity < OutOfStockThreshold);

            return new InventoryStatusData
            {
                Statuses = new List<string> { "In Stock", "Low Stock", "Out of Stock" },
                Counts = new List<int> { inStock, lowStock, outOfStock },
                Colors = new List<string> { "#10B981", "#F59E0B", "#EF4444" },
                TotalItems = inStock + lowStock + outOfStock
            };
        }

        private async Task<SalesByCategoryData> GetSalesByCategoryData(DateTime cutoffDate)
        {
            var salesByCategory = await _db.OrderDetails
                .Include(od => od.OrderHeader)
                .Include(od => od.Product)
                .ThenInclude(p => p.Category)
                .Where(od => od.OrderHeader.OrderDate >= cutoffDate)
                .GroupBy(od => od.Product.Category.Category_Name)
                .Select(g => new
                {
                    CategoryName = g.Key,
                    SalesCount = g.Sum(od => od.Count),
                    Revenue = (decimal)g.Sum(od => od.Count * od.price) // Explicitly cast to decimal
                })
                .OrderByDescending(x => x.Revenue)
                .ToListAsync();

            var colors = new List<string> { "#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6" };

            return new SalesByCategoryData
            {
                CategoryNames = salesByCategory.Select(x => x.CategoryName).ToList(),
                SalesCounts = salesByCategory.Select(x => x.SalesCount).ToList(),
                Revenue = salesByCategory.Select(x => x.Revenue).ToList(),
                Colors = colors.Take(salesByCategory.Count).ToList(),
                TotalSales = salesByCategory.Sum(x => x.SalesCount)
            };
        }

        private async Task<List<LowStockItem>> GetLowStockItems()
        {
            return await _db.Stocks
                .Include(s => s.Product)
                .ThenInclude(p => p.Category)
                .Include(s => s.Product.OrderDetails)
                .ThenInclude(od => od.OrderHeader)
                .Where(s => s.Quantity <= LowStockThreshold)
                .OrderBy(s => s.Quantity)
                .Select(s => new LowStockItem
                {
                    ProductName = s.Product.Product_Name,
                    Category = s.Product.Category.Category_Name,
                    Size = s.Size,
                    CurrentStock = s.Quantity,
                    Threshold = LowStockThreshold,
                    LastSoldDate = s.Product.OrderDetails
                        .OrderByDescending(od => od.OrderHeader.OrderDate)
                        .Select(od => (DateTime?)od.OrderHeader.OrderDate)
                        .FirstOrDefault()
                })
                .ToListAsync();
        }
    }
}
