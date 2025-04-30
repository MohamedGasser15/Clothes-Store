using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _db;
        protected readonly UserManager<ApplicationUser> _userManager;

        public BaseController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // Validates email format and checks for common TLDs
        public bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            try
            {
                var mailAddress = new System.Net.Mail.MailAddress(email);
                string[] parts = mailAddress.Host.Split('.');
                if (parts.Length < 2 || parts.Any(string.IsNullOrWhiteSpace))
                    return false;

                string[] commonTlds = { "com", "net", "org", "edu", "gov", "io", "co", "uk", "de", "fr" };
                string lastPart = parts.Last().ToLower();

                if (lastPart.Length < 2 || lastPart.Length > 6 || (!commonTlds.Contains(lastPart) && lastPart.Any(c => !char.IsLetter(c))))
                    return false;

                return mailAddress.Address == email;
            }
            catch
            {
                return false;
            }
        }

        // Tracks user device information and logs login activity
        public async Task TrackUserDevice(ApplicationUser user)
        {
            var deviceInfo = new
            {
                DeviceName = GetFriendlyDeviceName(Request.Headers["User-Agent"]),
                DeviceType = GetDeviceType(Request.Headers["User-Agent"]),
                IP = HttpContext.Connection.RemoteIpAddress?.ToString(),
                OS = GetOSFromUserAgent(Request.Headers["User-Agent"]),
                Browser = GetBrowserFromUserAgent(Request.Headers["User-Agent"]),
                Location = await GetLocationFromIP(HttpContext.Connection.RemoteIpAddress?.ToString())
            };

            var existingDevice = await _db.UserDevices
                .FirstOrDefaultAsync(d => d.UserId == user.Id && d.DeviceToken == Request.Cookies["DeviceToken"]);

            if (existingDevice != null)
            {
                existingDevice.LastLoginDate = DateTime.Now;
                var knownDeviceActivity = new SecurityActivity
                {
                    UserId = user.Id,
                    ActivityType = "KnownDeviceLogin",
                    Description = "Login detected from a previously used device.",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _db.SecurityActivities.Add(knownDeviceActivity);
                await _db.SaveChangesAsync();
            }
            else
            {
                var newDevice = new UserDevice
                {
                    UserId = user.Id,
                    DeviceName = deviceInfo.DeviceName,
                    DeviceType = deviceInfo.DeviceType,
                    IpAddress = deviceInfo.IP,
                    OS = deviceInfo.OS,
                    Browser = deviceInfo.Browser,
                    Location = deviceInfo.Location,
                    DeviceToken = Guid.NewGuid().ToString(),
                    FirstLoginDate = DateTime.Now,
                    LastLoginDate = DateTime.Now
                };

                _db.UserDevices.Add(newDevice);

                Response.Cookies.Append("DeviceToken", newDevice.DeviceToken, new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(30),
                    HttpOnly = true,
                    Secure = true
                });

                var newDeviceActivity = new SecurityActivity
                {
                    UserId = user.Id,
                    ActivityType = "NewDeviceLogin",
                    Description = "Login detected from a new device.",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _db.SecurityActivities.Add(newDeviceActivity);
                await _db.SaveChangesAsync();
            }
        }

        // Retrieves client IP address, handling forwarded IPs and IPv6
        public string GetClientIpAddress()
        {
            var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

            if (string.IsNullOrEmpty(ip))
            {
                ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            }
            else
            {
                ip = ip.Split(',')[0].Trim();
            }

            if (IPAddress.TryParse(ip, out var address))
            {
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    ip = address.MapToIPv4().ToString();
                }
                else if (ip == "::1")
                {
                    ip = "127.0.0.1";
                }
            }

            return ip ?? "Unknown";
        }

        // Determines operating system from user agent
        private string GetOSFromUserAgent(string userAgent)
        {
            if (userAgent.Contains("Windows")) return "Windows";
            if (userAgent.Contains("Mac")) return "MacOS";
            if (userAgent.Contains("Linux")) return "Linux";
            if (userAgent.Contains("Android")) return "Android";
            if (userAgent.Contains("iPhone")) return "iOS";
            return "Unknown";
        }

        // Identifies browser from user agent
        private string GetBrowserFromUserAgent(string userAgent)
        {
            if (userAgent.Contains("Edg")) return "Microsoft Edge";
            if (userAgent.Contains("Chrome")) return "Google Chrome";
            if (userAgent.Contains("Firefox")) return "Mozilla Firefox";
            if (userAgent.Contains("Safari") && !userAgent.Contains("Chrome")) return "Apple Safari";
            if (userAgent.Contains("Opera") || userAgent.Contains("OPR")) return "Opera";
            return "Unknown Browser";
        }

        // Generates a friendly device name based on user agent
        private string GetFriendlyDeviceName(string userAgent)
        {
            string deviceType = userAgent.Contains("Mobile") ? "Mobile" : "Desktop";

            if (userAgent.Contains("Windows NT")) deviceType = "Windows " + deviceType;
            else if (userAgent.Contains("Macintosh")) deviceType = "Mac " + deviceType;
            else if (userAgent.Contains("Linux")) deviceType = "Linux " + deviceType;

            string browser = GetBrowserFromUserAgent(userAgent);
            return $"{deviceType} ({browser})";
        }

        // Determines device type from user agent
        private string GetDeviceType(string userAgent)
        {
            if (userAgent.Contains("Mobi") || userAgent.Contains("Android")) return "Mobile";
            if (userAgent.Contains("Tablet") || userAgent.Contains("iPad")) return "Tablet";
            if (userAgent.Contains("Windows NT") || userAgent.Contains("Macintosh") || userAgent.Contains("Linux")) return "Desktop";
            if (userAgent.Contains("Xbox") || userAgent.Contains("PlayStation")) return "Gaming Console";
            return "Unknown Device";
        }

        // Retrieves location from IP address using external API
        private async Task<string> GetLocationFromIP(string ipAddress)
        {
            if (ipAddress == "::1" || ipAddress == "127.0.0.0")
                return "Cairo, Egypt (Local Development)";

            try
            {
                using var httpClient = new HttpClient();
                var response = await httpClient.GetFromJsonAsync<IpApiResponse>($"http://ip-api.com/json/{ipAddress}");
                return response.Status == "success" ? $"{response.City}, {response.Country}" : "Unknown Location";
            }
            catch
            {
                return "Location Unknown";
            }
        }

        // Record for IP API response
        private record IpApiResponse(string Status, string Country, string City);
    }
}