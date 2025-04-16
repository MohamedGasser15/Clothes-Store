using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Clothes_Store.Services
{
    // Services/Interfaces/IUserAnalyticsService.cs
    public interface IUserAnalyticsService
    {
        Task<UserAnalyticsViewModel> GetUserAnalytics(int days);
        Task<UserGrowthViewModel> GetUserGrowthData(DateTime startDate, DateTime endDate, int days);
    }

    // Services/UserAnalyticsService.cs
    public class UserAnalyticsService : IUserAnalyticsService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserAnalyticsService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<UserAnalyticsViewModel> GetUserAnalytics(int days)
        {
            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-days);

            var model = new UserAnalyticsViewModel
            {
                UserGrowth = await GetUserGrowthData(startDate, endDate, days),
                RolesDistribution = await GetRolesDistributionData(),
                AccountStatus = await GetAccountStatusData(),
                RegistrationSources = GetRegistrationSourcesData(),
                RecentActivity = await GetRecentActivityData(),
                TotalUsers = await _context.Users.CountAsync()
            };

            return model;
        }

        public async Task<UserGrowthViewModel> GetUserGrowthData(DateTime startDate, DateTime endDate, int days)
        {
            var interval = days <= 7 ? "day" : days <= 30 ? "week" : "month";

            var userGrowth = new UserGrowthViewModel
            {
                Labels = new List<string>(),
                NewUsers = new List<int>(),
                TotalUsers = new List<int>()
            };

            // Generate date labels
            var currentDate = startDate;
            while (currentDate <= endDate)
            {
                if (interval == "day")
                {
                    userGrowth.Labels.Add(currentDate.ToString("MMM d"));
                    currentDate = currentDate.AddDays(1);
                }
                else if (interval == "week")
                {
                    userGrowth.Labels.Add($"Week {GetWeekOfMonth(currentDate)}");
                    currentDate = currentDate.AddDays(7);
                }
                else
                {
                    userGrowth.Labels.Add(currentDate.ToString("MMM yyyy"));
                    currentDate = currentDate.AddMonths(1);
                }
            }

            // Get new users per interval
            currentDate = startDate;
            var totalUsers = 0;
            for (int i = 0; i < userGrowth.Labels.Count; i++)
            {
                DateTime nextDate = interval switch
                {
                    "day" => currentDate.AddDays(1),
                    "week" => currentDate.AddDays(7),
                    _ => currentDate.AddMonths(1)
                };

                var newUsersCount = await _context.ApplicationUsers
                    .Where(u => u.CreatedDate >= currentDate && u.CreatedDate < nextDate)
                    .CountAsync();

                totalUsers += newUsersCount;

                userGrowth.NewUsers.Add(newUsersCount);
                userGrowth.TotalUsers.Add(totalUsers);

                currentDate = nextDate;
            }

            return userGrowth;
        }

        private int GetWeekOfMonth(DateTime date)
        {
            date = date.Date;
            DateTime firstMonthDay = new DateTime(date.Year, date.Month, 1);
            DateTime firstMonthMonday = firstMonthDay.AddDays((DayOfWeek.Monday + 7 - firstMonthDay.DayOfWeek) % 7);
            if (firstMonthMonday > date)
            {
                firstMonthDay = firstMonthDay.AddMonths(-1);
                firstMonthMonday = firstMonthDay.AddDays((DayOfWeek.Monday + 7 - firstMonthDay.DayOfWeek) % 7);
            }
            return (date - firstMonthMonday).Days / 7 + 1;
        }

        private async Task<UserRolesDistributionViewModel> GetRolesDistributionData()
        {
            // 1. Get all existing roles from the database
            var existingRoles = await _context.Roles
                .Select(r => r.Name)
                .ToListAsync();

            // 2. Define role display names mapping
            var roleDisplayNames = new Dictionary<string, string>
    {
        {"Admin", "Administrator"},
        {"User", "Regular User"}
        // Add more mappings as needed
    };

            // 3. Handle case where there are many roles
            var roleCounts = new List<int>();
            var finalRoleNames = new List<string>();
            var colors = new List<string> { "#6366F1", "#10B981", "#3B82F6", "#F59E0B", "#EC4899", "#F97316" };

            const int otherRolesThreshold = 5; // Combine roles beyond this count into "Other"

            if (existingRoles.Count > otherRolesThreshold)
            {
                // Split into main roles and other roles
                var mainRoles = existingRoles.Take(otherRolesThreshold - 1).ToList();
                var otherRoles = existingRoles.Skip(otherRolesThreshold - 1).ToList();

                // Process main roles
                foreach (var role in mainRoles)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                    roleCounts.Add(usersInRole.Count);

                    // Apply display name if available
                    finalRoleNames.Add(
                        roleDisplayNames.TryGetValue(role, out var displayName)
                            ? displayName
                            : role
                    );
                }

                // Combine remaining roles into "Other"
                var otherCount = 0;
                foreach (var role in otherRoles)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                    otherCount += usersInRole.Count;
                }
                roleCounts.Add(otherCount);
                finalRoleNames.Add("Other");
            }
            else
            {
                // Process all roles normally
                foreach (var role in existingRoles)
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                    roleCounts.Add(usersInRole.Count);

                    // Apply display name if available
                    finalRoleNames.Add(
                        roleDisplayNames.TryGetValue(role, out var displayName)
                            ? displayName
                            : role
                    );
                }
            }

            // 4. Prepare colors (only use as many as we need)
            var roleColors = colors.Take(finalRoleNames.Count).ToList();

            return new UserRolesDistributionViewModel
            {
                RoleNames = finalRoleNames,
                Counts = roleCounts,
                Colors = roleColors
            };
        }

        private async Task<UserStatusViewModel> GetAccountStatusData()
        {
            var activeUsers = await _context.Users
                .Where(u => u.LockoutEnd == null || u.LockoutEnd < DateTime.Now)
                .CountAsync();

            var lockedUsers = await _context.Users
                .Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTime.Now)
                .CountAsync();

            return new UserStatusViewModel
            {
                ActiveUsers = activeUsers,
                LockedUsers = lockedUsers
            };
        }

        private RegistrationSourcesViewModel GetRegistrationSourcesData()
        {
            // In a real app, you would query this from your database
            // This is a simplified version
            return new RegistrationSourcesViewModel
            {
                Sources = new List<string> { "Website", "Mobile App", "Admin Created", "API" },
                Counts = new List<int> { 120, 80, 35, 15 },
                Colors = new List<string> { "#3B82F6", "#8B5CF6", "#10B981", "#F59E0B" }
            };
        }

        private async Task<RecentActivityViewModel> GetRecentActivityData()
        {
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            var last24Hours = DateTime.UtcNow.AddHours(-24);

            var newUsersToday = await _context.ApplicationUsers
                .Where(u => u.CreatedDate >= today)
                .CountAsync();

            // For logins, you would need to track login activity separately
            // This is a placeholder - you would need to implement actual login tracking
            var loginsLast24Hours = 124; // Placeholder

            // For purchases, you would query your order system
            var purchasesYesterday = 42; // Placeholder

            return new RecentActivityViewModel
            {
                NewUsersToday = newUsersToday,
                LoginsLast24Hours = loginsLast24Hours,
                PurchasesYesterday = purchasesYesterday
            };
        }
    }
}
