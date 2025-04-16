using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clothes_Models.ViewModels
{
    // ViewModels/UserAnalyticsViewModels.cs
    public class UserAnalyticsViewModel
    {
        public UserGrowthViewModel UserGrowth { get; set; }
        public UserRolesDistributionViewModel RolesDistribution { get; set; }
        public UserStatusViewModel AccountStatus { get; set; }
        public RegistrationSourcesViewModel RegistrationSources { get; set; }
        public RecentActivityViewModel RecentActivity { get; set; }
        public int TotalUsers { get; set; }
    }

    public class UserGrowthViewModel
    {
        public List<string> Labels { get; set; }
        public List<int> NewUsers { get; set; }
        public List<int> TotalUsers { get; set; }
    }

    public class UserRolesDistributionViewModel
    {
        public List<string> RoleNames { get; set; }
        public List<int> Counts { get; set; }
        public List<string> Colors { get; set; }
    }

    public class UserStatusViewModel
    {
        public int ActiveUsers { get; set; }
        public int LockedUsers { get; set; }
    }

    public class RegistrationSourcesViewModel
    {
        public List<string> Sources { get; set; }
        public List<int> Counts { get; set; }
        public List<string> Colors { get; set; }
    }

    public class RecentActivityViewModel
    {
        public int NewUsersToday { get; set; }
        public int LoginsLast24Hours { get; set; }
        public int PurchasesYesterday { get; set; }
    }
}
