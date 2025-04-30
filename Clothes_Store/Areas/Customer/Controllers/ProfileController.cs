using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class ProfileController : BaseController
    {
        private readonly IEmailSender _emailSender;

        // Constructor: Initializes database, user manager, and email sender
        public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailSender emailSender)
            : base(db, userManager)
        {
            _emailSender = emailSender;
        }

        // Displays the user's profile information
        public IActionResult Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var profile = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userId);

            if (profile == null)
                return RedirectToAction("Edit");

            var model = new ProfileViewModel
            {
                Id = profile.Id,
                Name = profile.Name,
                Email = profile.Email,
                PhoneNumber = profile.PhoneNumber,
                Address = profile.StreetAddress,
                PostalCode = profile.PostalCode,
                Country = profile.Country
            };

            return View(model);
        }

        // Updates the user's name
        [HttpPost]
        public IActionResult UpdateName(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _userManager.FindByIdAsync(userId).Result;

            if (user == null)
                return NotFound();

            user.Name = model.Name;
            _userManager.UpdateAsync(user).Wait();
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Name updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Updates the user's email with validation
        [HttpPost]
        public IActionResult UpdateEmail(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _userManager.FindByIdAsync(userId).Result;

            if (user == null)
                return NotFound();

            if (!IsValidEmail(model.Email))
            {
                TempData["ErrorMessage"] = "Invalid email address!";
                return RedirectToAction(nameof(Index));
            }

            var existingUser = _userManager.FindByEmailAsync(model.Email).Result;
            if (existingUser != null && existingUser.Id != user.Id)
            {
                TempData["ErrorMessage"] = "Email address already in use!";
                return RedirectToAction(nameof(Index));
            }

            user.Email = model.Email;
            user.EmailConfirmed = false;
            _userManager.UpdateAsync(user).Wait();
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Email updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Updates the user's phone number
        [HttpPost]
        public IActionResult UpdatePhone(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _userManager.FindByIdAsync(userId).Result;

            if (user == null)
                return NotFound();

            user.PhoneNumber = model.PhoneNumber;
            _userManager.UpdateAsync(user).Wait();
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Phone number updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Updates the user's address
        [HttpPost]
        public IActionResult UpdateAddress(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _userManager.FindByIdAsync(userId).Result;

            if (user == null)
                return NotFound();

            user.StreetAddress = model.Address;
            _userManager.UpdateAsync(user).Wait();
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Address updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Updates the user's postal code
        [HttpPost]
        public IActionResult UpdatePostalCode(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _userManager.FindByIdAsync(userId).Result;

            if (user == null)
                return NotFound();

            user.PostalCode = model.PostalCode;
            _userManager.UpdateAsync(user).Wait();
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Postal code updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Updates the user's country
        [HttpPost]
        public IActionResult UpdateCountry(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = _userManager.FindByIdAsync(userId).Result;

            if (user == null)
                return NotFound();

            user.Country = model.Country;
            _userManager.UpdateAsync(user).Wait();
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Country updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // Displays security settings and recent activities
        public IActionResult Security()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            var model = new ProfileViewModel
            {
                Email = user.Email,
                IsEmailConfirmed = user.EmailConfirmed,
                IsTwoFactorEnabled = _userManager.GetTwoFactorEnabledAsync(user).Result,
                ConnectedDevices = _db.UserDevices
                    .Where(d => d.UserId == user.Id)
                    .OrderByDescending(d => d.LastLoginDate)
                    .Take(2)
                    .ToList(),
                RecentSecurityActivities = _db.SecurityActivities
                    .Where(a => a.UserId == user.Id)
                    .OrderByDescending(a => a.ActivityDate)
                    .Take(5)
                    .ToList()
            };
            return View(model);
        }

        // Sends email verification code
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendEmailVerificationCode()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return View("Error");

            var verificationCode = GenerateSecureSixDigitCode();
            var emailBody = GenerateEmailConfirmationEmail(user, verificationCode);
            TempData["EmailVerificationCode"] = verificationCode;

            _emailSender.SendEmailAsync(user.Email, "Confirm your email", emailBody).Wait();
            return RedirectToAction("VerifyEmailCode", new { userId = user.Id });
        }

        // Displays page to input email verification code
        [HttpGet]
        public IActionResult VerifyEmailCode(string userId)
        {
            var user = _userManager.FindByIdAsync(userId).Result;
            if (user == null)
                return NotFound();

            return View(new VerifyEmailCodeViewModel { UserId = user.Id, Email = user.Email });
        }

        // Verifies email confirmation code
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VerifyEmailCode(VerifyEmailCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var user = _userManager.FindByIdAsync(model.UserId).Result;
                if (user != null)
                    model.Email = user.Email;
                ModelState.AddModelError("Code", "Please enter a valid 6-digit code.");
                return View(model);
            }

            var storedCode = TempData.Peek("EmailVerificationCode")?.ToString();
            if (string.IsNullOrEmpty(storedCode) || storedCode != model.Code || string.IsNullOrEmpty(model.UserId))
            {
                var invalidCodeUser = _userManager.FindByIdAsync(model.UserId).Result;
                if (invalidCodeUser != null)
                    model.Email = invalidCodeUser.Email;
                ModelState.AddModelError("Code", "Invalid or expired verification code.");
                return View(model);
            }

            TempData.Remove("EmailVerificationCode");
            var verifiedUser = _userManager.FindByIdAsync(model.UserId).Result;
            if (verifiedUser == null)
                return View("Error");

            verifiedUser.EmailConfirmed = true;
            var result = _userManager.UpdateAsync(verifiedUser).Result;
            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Failed to verify email. Please try again.");
                model.Email = verifiedUser.Email;
                return View(model);
            }

            return RedirectToAction("Security", new { area = "Customer", message = "Email verified successfully!" });
        }

        // Displays the change password page
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }

        // Processes password change with security logging
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var changeTime = DateTime.Now;

            var activity = new SecurityActivity
            {
                UserId = user.Id,
                ActivityType = "PasswordChange",
                Description = "Password changed",
                IpAddress = ipAddress
            };
            _db.SecurityActivities.Add(activity);
            _db.SaveChanges();

            var result = _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword).Result;
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            var resetToken = _userManager.GeneratePasswordResetTokenAsync(user).Result;
            var passwordResetLink = Url.Action("ResetPassword", "Account", new { userId = user.Id, code = resetToken }, HttpContext.Request.Scheme);
            var emailBody = GeneratePasswordChangeEmail(user, ipAddress, System.Net.Dns.GetHostName(), changeTime, passwordResetLink);

            _emailSender.SendEmailAsync(user.Email, "Your password has been changed", emailBody).Wait();
            TempData["SuccessMessage"] = "Password changed successfully!";
            return RedirectToAction("Security");
        }

        // Initiates two-factor authentication setup
        [HttpGet]
        public IActionResult Enable2FA()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            if (!user.EmailConfirmed)
            {
                TempData["ErrorMessage"] = "Please confirm your email first";
                return RedirectToAction("Security");
            }

            var code = GenerateSecureSixDigitCode();
            var emailBody = GenerateEmailEnable2FA(user, code);

            HttpContext.Session.SetString("2FA_Code", code);
            HttpContext.Session.SetString("2FA_User", user.Id);
            HttpContext.Session.SetString("2FA_Expiry", DateTime.Now.AddMinutes(5).ToString());
            _emailSender.SendEmailAsync(user.Email, "Enable 2FA - Verification Code", emailBody).Wait();

            return View(new TwoFactorSetupViewModel { Email = user.Email });
        }

        // Verifies 2FA code and enables two-factor authentication
        [HttpPost]
        public IActionResult Verify2FACode(string code)
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            var expiry = DateTime.Parse(HttpContext.Session.GetString("2FA_Expiry"));
            if (DateTime.Now > expiry)
            {
                TempData["ErrorMessage"] = "Code has expired";
                return RedirectToAction("Enable2FA");
            }

            var storedCode = HttpContext.Session.GetString("2FA_Code");
            if (string.Equals(code?.Trim(), storedCode, StringComparison.OrdinalIgnoreCase))
            {
                _userManager.SetTwoFactorEnabledAsync(user, true).Wait();
                HttpContext.Session.Remove("2FA_Code");
                HttpContext.Session.Remove("2FA_Expiry");
                TempData["SuccessMessage"] = "Two-factor authentication enabled successfully!";
                return RedirectToAction("Security");
            }

            TempData["ErrorMessage"] = "Invalid verification code";
            return RedirectToAction("Enable2FA");
        }

        // Disables two-factor authentication
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Disable2FA()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            var result = _userManager.SetTwoFactorEnabledAsync(user, false).Result;
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to disable 2FA. Please try again.";
                return RedirectToAction("Security");
            }

            TempData["SuccessMessage"] = "Two-factor authentication disabled successfully!";
            return RedirectToAction("Security");
        }

        // Resends 2FA verification code
        [HttpPost]
        public IActionResult Resend2FACode()
        {
            return RedirectToAction("Enable2FA");
        }

        // Displays connected devices for the user
        [HttpGet]
        public IActionResult ManageDevices()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            var devices = _db.UserDevices
                .Where(d => d.UserId == user.Id)
                .OrderByDescending(d => d.LastLoginDate)
                .ToList();

            return View(devices);
        }

        // Removes a specific device
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveDevice(string deviceId)
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            var device = _db.UserDevices
                .FirstOrDefault(d => d.Id == Guid.Parse(deviceId) && d.UserId == user.Id);

            if (device == null)
            {
                TempData["Error"] = "Device not found";
                return RedirectToAction("ManageDevices");
            }

            _userManager.UpdateSecurityStampAsync(user).Wait();
            _db.UserDevices.Remove(device);
            _db.SaveChanges();

            TempData["Success"] = "Device removed successfully!";
            return RedirectToAction("ManageDevices");
        }

        // Removes inactive devices older than 30 days
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveInactiveDevices()
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return NotFound();

            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var inactiveDevices = _db.UserDevices
                .Where(d => d.UserId == user.Id &&
                           d.LastLoginDate < thirtyDaysAgo &&
                           d.DeviceToken != Request.Cookies["DeviceToken"])
                .ToList();

            if (inactiveDevices.Any())
            {
                _userManager.UpdateSecurityStampAsync(user).Wait();
                _db.UserDevices.RemoveRange(inactiveDevices);
                _db.SaveChanges();
                TempData["Success"] = $"Removed {inactiveDevices.Count} inactive devices!";
            }
            else
            {
                TempData["Info"] = "No inactive devices to remove";
            }

            return RedirectToAction("ManageDevices");
        }

        // Displays the payments page
        public IActionResult Payments()
        {
            return View();
        }

        // Displays paginated user orders
        [HttpGet]
        public IActionResult Orders(int page = 1, int pageSize = 5)
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
                return RedirectToPage("/Account/Login");

            var totalOrders = _db.OrderHeaders.Count(oh => oh.ApplicationUserId == user.Id);
            var orderHeaders = _db.OrderHeaders
                .Where(oh => oh.ApplicationUserId == user.Id)
                .OrderByDescending(oh => oh.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var orderDetails = _db.OrderDetails
                .Include(od => od.Product)
                .Where(od => orderHeaders.Select(oh => oh.Id).Contains(od.OrderHeaderId))
                .ToList();

            var orderVMs = orderHeaders.Select(oh => new OrderVM
            {
                OrderHeader = oh,
                OrderDetails = orderDetails.Where(od => od.OrderHeaderId == oh.Id).ToList()
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);
            ViewBag.PageSize = pageSize;

            return View(orderVMs);
        }

        // Cancels an order with refund processing
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelOrder(int id)
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
            {
                TempData["Error"] = "You must be logged in to cancel an order";
                return RedirectToPage("/Account/Login");
            }

            var orderHeader = _db.OrderHeaders
                .Include(o => o.ApplicationUser)
                .FirstOrDefault(o => o.Id == id && o.ApplicationUserId == user.Id);

            if (orderHeader == null)
            {
                TempData["Error"] = "Order not found or unauthorized";
                return RedirectToAction(nameof(Orders));
            }

            if (orderHeader.OrderStatus != SD.StatusPending && orderHeader.OrderStatus != SD.StatusApproved)
            {
                TempData["Error"] = $"Cannot cancel order with status: {orderHeader.OrderStatus}";
                return RedirectToAction(nameof(Orders));
            }

            if (!string.IsNullOrEmpty(orderHeader.PaymentIntentId))
            {
                var refundService = new RefundService();
                var refundOptions = new RefundCreateOptions
                {
                    PaymentIntent = orderHeader.PaymentIntentId,
                    Reason = RefundReasons.RequestedByCustomer
                };

                try
                {
                    refundService.CreateAsync(refundOptions).Wait();
                    orderHeader.PaymentStatus = SD.StatusRefunded;
                }
                catch (StripeException)
                {
                    TempData["Error"] = "Refund failed. Please check Stripe dashboard.";
                    return RedirectToAction(nameof(Orders));
                }
            }

            orderHeader.OrderStatus = SD.StatusCancelled;

            if (user != null)
            {
                var emailBody = GenerateEmailCancelled(user, orderHeader.Id);

                 _emailSender.SendEmailAsync(
                    user.Email,
                    "Your Order Has Been Cancelled",
                    emailBody
                );
            }
            else
            {
                TempData["Error"] = $"User for order #{orderHeader.Id} not found!";
                return RedirectToAction(nameof(Index));
            }
            _db.SaveChanges();

            TempData["Success"] = $"Order #{orderHeader.Id} cancelled successfully";
            return RedirectToAction(nameof(Orders));
        }

        // Displays the contact us page
        public IActionResult ContactUs()
        {
            return View();
        }
        // Generates HTML email for email verification
        private string GenerateEmailConfirmationEmail(ApplicationUser user, string code)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 1px solid #eaeaea; padding-bottom: 15px; }}
        .header h1 {{ color: #088178; margin: 0; font-size: 24px; }}
        .content {{ margin-bottom: 25px; line-height: 1.6; }}
        .content p {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .verification-code {{ font-size: 28px; font-weight: bold; color: #088178; letter-spacing: 3px; text-align: center; margin: 25px 0; padding: 15px; background-color: #f8f9fa; border-radius: 6px; border: 1px dashed #088178; }}
        .security-alert {{ background-color: #f8f9fa; border-left: 4px solid #088178; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; font-size: 14px; color: #777; margin-top: 25px; border-top: 1px solid #eaeaea; padding-top: 15px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #088178; color: white; text-decoration: none; border-radius: 4px; margin: 20px auto; text-align: center; }}
        .info-item {{ margin-bottom: 8px; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'><h1>Email Verification</h1></div>
        <div class='content'>
            <p>Hello {user.Name},</p>
            <p>Thank you for registering with LushThreads! Please use the following verification code to confirm your email address:</p>
            <div class='verification-code'>{code}</div>
            <p>This code will expire in 15 minutes. If you didn't request this, please ignore this email.</p>
            <div class='security-alert'><p><strong>Security Tip:</strong> Never share this code with anyone. LushThreads will never ask for your verification code.</p></div>
            <p>Alternatively, you can click the button below to verify your email:</p>
            <a href='{GenerateVerificationLink(user, code)}' class='button'>Verify Email Address</a>
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style='word-break: break-all;'>{GenerateVerificationLink(user, code)}</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our verification process.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates verification link for email
        private string GenerateVerificationLink(ApplicationUser user, string code)
        {
            return Url.Action("VerifyEmailCode", "Profile", new { userId = user.Id, code }, HttpContext.Request.Scheme);
        }

        // Generates HTML email for 2FA setup
        private string GenerateEmailEnable2FA(ApplicationUser user, string code)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 1px solid #eaeaea; padding-bottom: 15px; }}
        .header h1 {{ color: #088178; margin: 0; font-size: 24px; }}
        .content {{ margin-bottom: 25px; line-height: 1.6; }}
        .content p {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .verification-code {{ font-size: 28px; font-weight: bold; color: #088178; letter-spacing: 3px; text-align: center; margin: 25px 0; padding: 15px; background-color: #f8f9fa; border-radius: 6px; border: 1px dashed #088178; }}
        .security-alert {{ background-color: #f8f9fa; border-left: 4px solid #088178; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; font-size: 14px; color: #777; margin-top: 25px; border-top: 1px solid #eaeaea; padding-top: 15px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #088178; color: white; text-decoration: none; border-radius: 4px; margin: 20px auto; text-align: center; }}
        .info-item {{ margin-bottom: 8px; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'><h1>Two-Factor Authentication Code</h1></div>
        <div class='content'>
            <p>Hello {user.Name},</p>
            <p>Your login attempt requires verification. Use this code to complete your sign-in:</p>
            <div class='verification-code'>{code}</div>
            <p>This code will expire in 15 minutes. If you didn't request this, please ignore this email.</p>
            <div class='security-alert'><p><strong>Security Tip:</strong> Never share this code with anyone. LushThreads will never ask for your verification code.</p></div>
            <p>Alternatively, you can click the button below to verify your Two-Factor Authentication:</p>
            <a href='{GenerateEnable2FALink(user, code)}' class='button'>Verify Email Address</a>
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style='word-break: break-all;'>{GenerateEnable2FALink(user, code)}</p>
        </div>
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our verification process.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates 2FA verification link
        private string GenerateEnable2FALink(ApplicationUser user, string code)
        {
            return Url.Action("Enable2FA", "Profile", new { userId = user.Id, code }, HttpContext.Request.Scheme);
        }

        // Generates HTML email for password change confirmation
        private string GeneratePasswordChangeEmail(ApplicationUser user, string ipAddress, string deviceName, DateTime changeTime, string passwordResetLink)
        {
            return $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .email-container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; padding: 30px; border-radius: 8px; box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1); }}
        .header {{ text-align: center; margin-bottom: 25px; border-bottom: 1px solid #eaeaea; padding-bottom: 15px; }}
        .header h1 {{ color: #088178; margin: 0; font-size: 24px; }}
        .content {{ margin-bottom: 25px; line-height: 1.6; }}
        .content p {{ font-size: 16px; color: #333333; margin-bottom: 15px; }}
        .security-alert {{ background-color: #f8f9fa; border-left: 4px solid #088178; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; font-size: 14px; color: #777; margin-top: 25px; border-top: 1px solid #eaeaea; padding-top: 15px; }}
        .button {{ display: inline-block; padding: 10px 20px; background-color: #088178; color: white; text-decoration: none; border-radius: 4px; margin: 15px 0; }}
        .info-item {{ margin-bottom: 8px; }}
        .info-label {{ font-weight: bold; color: #555; }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'><h1>Password Change Confirmation</h1></div>
        <div class='content'>
            <p>Hello {user.Name},</p>
            <p>Your LushThreads account password was successfully changed on {changeTime:MMMM dd, yyyy} at {changeTime:h:mm tt}.</p>
            <div class='security-alert'><p><strong>Security Notice:</strong> If you didn't make this change, please take immediate action to secure your account.</p></div>
            <div class='info-item'><span class='info-label'>Device:</span> {deviceName}</div>
            <div class='info-item'><span class='info-label'>IP Address:</span> {ipAddress}</div>
            <div class='info-item'><span class='info-label'>Time:</span> {changeTime:f}</div>
            <p>For your security, we recommend that you:</p>
            <ul>
                <li>Use a strong, unique password</li>
                <li>Enable two-factor authentication</li>
                <li>Review your recent account activity</li>
            </ul>
            <a href='{passwordResetLink}' class='button'>Secure My Account</a>
        </div>
        <div class='footer'>
            <p>© {changeTime.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our security notifications.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates HTML email for Order Cancellation
        private string GenerateEmailCancelled(ApplicationUser user, int orderNumber)
        {
            return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: Arial, sans-serif;
            background-color: #f4f4f4;
            margin: 0;
            padding: 20px;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 30px;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 25px;
            border-bottom: 1px solid #eaeaea;
            padding-bottom: 15px;
        }}
        .header h1 {{
            color: #088178;
            margin: 0;
            font-size: 24px;
        }}
        .content {{
            margin-bottom: 25px;
            line-height: 1.6;
        }}
        .content p {{
            font-size: 16px;
            color: #333333;
            margin-bottom: 15px;
        }}
        .order-number {{
            font-size: 28px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #e6f4f1;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .info-box {{
            background-color: #e6f4f1;
            border-left: 4px solid #088178;
            padding: 15px;
            margin: 20px 0;
            border-radius: 4px;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #777;
            margin-top: 25px;
            border-top: 1px solid #eaeaea;
            padding-top: 15px;
        }}
        .button {{
            display: inline-block;
            padding: 12px 24px;
            background-color: #088178;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px auto;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Your Order Has Been Cancelled</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>You have cancelled your order. Please find the details below:</p>
            
            <div class='order-number'>
                Order #{orderNumber}
            </div>
            
            <p>If you cancelled this order by mistake or need further assistance, please contact our support team.</p>
            
            <div class='info-box'>
                <p>We’re here to help if you have any questions or need to place a new order.</p>
            </div>

            <p>You can view your order history through your account:</p>

            <p style=""text-align:center; margin-top:20px;"">
                <a href=""{GenerateEmailLink(user)}"" class='button'>View My Orders</a>
            </p>

            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateEmailLink(user)}</p>
        </div>
        
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} regarding your cancelled order.</p>
        </div>
    </div>
</body>
</html>";
        }

        // Generates Link For Email
        private string GenerateEmailLink(ApplicationUser user)
        {
            return Url.Action(
                "Orders",
                "Profile",
                new { area = "Customer", userId = user.Id },
                protocol: HttpContext.Request.Scheme
            );
        }
        // Validates email format
        private static bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        // Generates a secure 6-digit code for verification
        private static string GenerateSecureSixDigitCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            return (BitConverter.ToUInt32(bytes, 0) % 1000000).ToString("D6");
        }
    }
}