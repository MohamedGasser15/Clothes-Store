using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class ProfileController : BaseController
    {
        private readonly IEmailSender _emailSender;

        public ProfileController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEmailSender emailSender) : base(db, userManager)
        {
            _emailSender = emailSender;
        }
        public async Task<IActionResult> Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier).Value; // Get the current user's ID

            var profile = await _db.ApplicationUsers.Where(u => u.Id == userId).FirstOrDefaultAsync();

            if (profile == null)
            {
                return RedirectToAction("Edit");
            }
            var model = new ProfileViewModel
            {
                Id = profile.Id,
                Name = profile.Name,
                Email = profile.Email,
                PhoneNumber = profile.PhoneNumber,
                Address = profile.StreetAddress,
                PostalCode = profile.PostalCode,
                Country = profile.Country,
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateName(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.Name = model.Name;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Last Name updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateEmail(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            // Validate email format
            if (!IsValidEmail(model.Email))
            {
                TempData["ErrorMessage"] = "Please enter a valid email address!";
                return RedirectToAction(nameof(Index));
            }

            // Check if the email already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                TempData["ErrorMessage"] = "The email address is already in use!";
                return RedirectToAction(nameof(Index));
            }

            // Otherwise, update the email
            user.Email = model.Email;
            user.EmailConfirmed = false;

            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Email updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePhone(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }
            user.PhoneNumber = model.PhoneNumber;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Phone Number updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateAddress(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.StreetAddress = model.Address;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Address updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePostalCode(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.PostalCode = model.PostalCode;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Postal Code updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateCountry(ProfileViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            user.Country = model.Country;
            await _userManager.UpdateAsync(user);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "City updated successfully!";
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Security()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                Email = user.Email,
                IsEmailConfirmed = user.EmailConfirmed,
                IsTwoFactorEnabled = await _userManager.GetTwoFactorEnabledAsync(user),
                ConnectedDevices = await _db.UserDevices
                    .Where(d => d.UserId == user.Id)
                    .OrderByDescending(d => d.LastLoginDate)
                    .Take(2)
                    .ToListAsync(),
                RecentSecurityActivities = await _db.SecurityActivities
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .ToListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmailVerificationCode()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View("Error");
            }

            var verificationCode = new Random().Next(100000, 999999).ToString();
            var emailBody = GenerateEmailConfirmationEmail(user, verificationCode);
            TempData["EmailVerificationCode"] = verificationCode;

            try
            {
                await _emailSender.SendEmailAsync(user.Email, "Confirm your email", emailBody);
                Console.WriteLine("Email sent successfully to: " + user.Email);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error sending email: " + ex.Message);
            }

            return RedirectToAction("VerifyEmailCode", new { userId = user.Id });
        }
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmailCode(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var model = new VerifyEmailCodeViewModel
            {
                UserId = user.Id,
                Email = user.Email
            };

            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailCode(VerifyEmailCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user != null) model.Email = user.Email;
                ModelState.AddModelError("Code", "Please enter a valid 6-digit code.");
                return View(model);
            }

            var storedCode = TempData.Peek("EmailVerificationCode")?.ToString();

            if (string.IsNullOrEmpty(storedCode)
                || storedCode != model.Code
                || string.IsNullOrEmpty(model.UserId))
            {
                var invalidCodeUser = await _userManager.FindByIdAsync(model.UserId);
                if (invalidCodeUser != null) model.Email = invalidCodeUser.Email;

                ModelState.AddModelError("Code", "The verification code is invalid or has expired.");
                return View(model);
            }

            TempData.Remove("EmailVerificationCode");

            var verifiedUser = await _userManager.FindByIdAsync(model.UserId);
            if (verifiedUser == null)
            {
                return View("Error");
            }

            verifiedUser.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(verifiedUser);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Failed to verify email. Please try again.");
                model.Email = verifiedUser.Email;
                return View(model);
            }

            return RedirectToAction("Security", "Profile", new { area = "Customer", message = "Email verified successfully!" });
        }

        private string GenerateEmailConfirmationEmail(ApplicationUser user, string confirmationCode)
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
            color: #6366f1;
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
        .verification-code {{
            font-size: 28px;
            font-weight: bold;
            color: #6366f1;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px dashed #6366f1;
        }}
        .security-alert {{
            background-color: #f8f9fa;
            border-left: 4px solid #6366f1;
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
            background-color: #6366f1;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px auto;
            text-align: center;
        }}
        .info-item {{
            margin-bottom: 8px;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Email Verification</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>Thank you for registering with Trendsvalley! Please use the following verification code to confirm your email address:</p>
            
            <div class='verification-code'>
                {confirmationCode}
            </div>
            
            <p>This code will expire in 15 minutes. If you didn't request this, please ignore this email.</p>
            
            <div class='security-alert'>
                <p><strong>Security Tip:</strong> Never share this code with anyone. Trendsvalley will never ask for your verification code.</p>
            </div>
            
            <p>Alternatively, you can click the button below to verify your email:</p>
            
            <a href=""{GenerateVerificationLink(user, confirmationCode)}"" class='button'>Verify Email Address</a>
            
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateVerificationLink(user, confirmationCode)}</p>
        </div>
        
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} Cara-Store. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our verification process.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateVerificationLink(ApplicationUser user, string code)
        {
            return Url.Action(
                "VerifyEmailCode",
                "Profile",
                new { userId = user.Id, code = code },
                protocol: HttpContext.Request.Scheme
            );
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordViewModel());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var ipAddress = GetClientIpAddress();
            var deviceName = System.Net.Dns.GetHostName();
            var changeTime = DateTime.Now;

            var activity = new SecurityActivity
            {
                UserId = user.Id,
                ActivityType = "PasswordChange",
                Description = "Password changed",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };
            _db.SecurityActivities.Add(activity);
            await _db.SaveChangesAsync();
            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { userId = user.Id, code = resetToken },
                protocol: HttpContext.Request.Scheme
            );

            var emailSubject = "Your password has been changed";
            var emailBody = GeneratePasswordChangeEmail(user, ipAddress, deviceName, changeTime, passwordResetLink);
            await _emailSender.SendEmailAsync(user.Email, emailSubject, emailBody);

            TempData["SuccessMessage"] = "Your password has been changed successfully!";
            return RedirectToAction("Security");
        }

        private string GeneratePasswordChangeEmail(
            ApplicationUser user,
            string ipAddress,
            string deviceName,
            DateTime changeTime,
            string passwordResetLink)
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
            color: #6366f1;
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
        .security-alert {{
            background-color: #f8f9fa;
            border-left: 4px solid #6366f1;
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
            padding: 10px 20px;
            background-color: #6366f1;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 15px 0;
        }}
        .info-item {{
            margin-bottom: 8px;
        }}
        .info-label {{
            font-weight: bold;
            color: #555;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Password Change Confirmation</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>Your TrendsValley account password was successfully changed on {changeTime:MMMM dd, yyyy} at {changeTime:h:mm tt}.</p>
            
            <div class='security-alert'>
                <p><strong>Security Notice:</strong> If you didn't make this change, please take immediate action to secure your account.</p>
            </div>
            
            <div class='info-item'>
                <span class='info-label'>Device:</span> {deviceName}
            </div>
            <div class='info-item'>
                <span class='info-label'>IP Address:</span> {ipAddress}
            </div>
            <div class='info-item'>
                <span class='info-label'>Time:</span> {changeTime:f}
            </div>
            
            <p>For your security, we recommend that you:</p>
            <ul>
                <li>Use a strong, unique password</li>
                <li>Enable two-factor authentication</li>
                <li>Review your recent account activity</li>
            </ul>
            
            <a href=""{passwordResetLink}"" class='button'>Secure My Account</a>
        </div>
        
        <div class='footer'>
            <p>&copy; {changeTime.Year} TrendsValley. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our security notifications.</p>
        </div>
    </div>
</body>
</html>";

        }

        [HttpGet]
        public async Task<IActionResult> Enable2FA()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            if (!user.EmailConfirmed)
            {
                TempData["ErrorMessage"] = "Please confirm your email first";
                return RedirectToAction("Security");
            }

            var code = GenerateSecureSixDigitCode();
            var emailBody = GenerateEmailEnable2FA(user, code);

            HttpContext.Session.SetString("2FA_Code", code);
            HttpContext.Session.SetString("2FA_User", user.Id);
            HttpContext.Session.SetString("2FA_Expiry",
                DateTime.Now.AddMinutes(5).ToString(CultureInfo.InvariantCulture));
            await _emailSender.SendEmailAsync(
                user.Email,
                "Enable 2FA - Verification Code",
                emailBody
            );

            return View(new TwoFactorSetupViewModel { Email = user.Email });
        }
        private string GenerateEmailEnable2FA(ApplicationUser user, string confirmationCode)
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
            color: #6366f1;
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
        .verification-code {{
            font-size: 28px;
            font-weight: bold;
            color: #6366f1;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px dashed #6366f1;
        }}
        .security-alert {{
            background-color: #f8f9fa;
            border-left: 4px solid #6366f1;
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
            background-color: #6366f1;
            color: white;
            text-decoration: none;
            border-radius: 4px;
            margin: 20px auto;
            text-align: center;
        }}
        .info-item {{
            margin-bottom: 8px;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Two-Factor Authentication Code</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p>Your login attempt requires verification. Use this code to complete your sign-in:</p>
            
            <div class='verification-code'>
                {confirmationCode}
            </div>
            
            <p>This code will expire in 15 minutes. If you didn't request this, please ignore this email.</p>
            
            <div class='security-alert'>
                <p><strong>Security Tip:</strong> Never share this code with anyone. Trendsvalley will never ask for your verification code.</p>
            </div>
            
            <p>Alternatively, you can click the button below to verify your Two-Factor Authentication:</p>
            
            <a href=""{GenerateEnable2FALink(user, confirmationCode)}"" class='button'>Verify Email Address</a>
            
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateEnable2FALink(user, confirmationCode)}</p>
        </div>
        
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} TrendsValley. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our verification process.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateEnable2FALink(ApplicationUser user, string code)
        {
            return Url.Action(
                "Enable2FA",
                "Profile",
                new { userId = user.Id, code = code },
                protocol: HttpContext.Request.Scheme
            );
        }

        [HttpPost]
        public async Task<IActionResult> Verify2FACode(string code)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var expiry = DateTime.Parse(HttpContext.Session.GetString("2FA_Expiry"));
            if (DateTime.Now > expiry)
            {
                TempData["ErrorMessage"] = "Code has expired";
                return RedirectToAction("Enable2FA");
            }

            var storedCode = HttpContext.Session.GetString("2FA_Code");
            if (string.Equals(code?.Trim(), storedCode, StringComparison.OrdinalIgnoreCase))
            {
                await _userManager.SetTwoFactorEnabledAsync(user, true);

                HttpContext.Session.Remove("2FA_Code");
                HttpContext.Session.Remove("2FA_Expiry");

                TempData["SuccessMessage"] = "Two-factor authentication enabled successfully!";
                return RedirectToAction("Security");
            }

            TempData["ErrorMessage"] = "Invalid verification code";
            return RedirectToAction("Enable2FA");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable2FA()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var disableResult = await _userManager.SetTwoFactorEnabledAsync(user, false);

            if (!disableResult.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to disable 2FA. Please try again.";
                return RedirectToAction("Security");
            }

            TempData["SuccessMessage"] = "Two-factor authentication has been disabled. You have been logged out for security reasons.";
            return RedirectToAction("Security");

        }

        [HttpPost]
        public async Task<IActionResult> Resend2FACode()
        {
            await Task.Delay(1000);
            return RedirectToAction("Enable2FA");
        }

        [HttpGet]
        public async Task<IActionResult> ManageDevices()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var devices = await _db.UserDevices
                .Where(d => d.UserId == user.Id)
                .OrderByDescending(d => d.LastLoginDate)
                .ToListAsync();

            return View(devices);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveDevice(string deviceId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var device = await _db.UserDevices
                .FirstOrDefaultAsync(d => d.Id == Guid.Parse(deviceId) && d.UserId == user.Id);

            if (device == null)
            {
                TempData["Error"] = "The Device Is Not Found";
                return RedirectToAction("ManageDevices");
            }


            await _userManager.UpdateSecurityStampAsync(user);

            _db.UserDevices.Remove(device);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Device removed and logout activated successfully!";
            return RedirectToAction("ManageDevices");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveInactiveDevices()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var inactiveDevices = await _db.UserDevices
                .Where(d => d.UserId == user.Id &&
                           d.LastLoginDate < thirtyDaysAgo &&
                           d.DeviceToken != Request.Cookies["DeviceToken"])
                .ToListAsync();

            if (inactiveDevices.Any())
            {
                await _userManager.UpdateSecurityStampAsync(user);

                _db.UserDevices.RemoveRange(inactiveDevices);
                await _db.SaveChangesAsync();

                TempData["Success"] = $"Removed {inactiveDevices.Count} inactive devices and activated logout!";
            }
            else
            {
                TempData["Info"] = "No inactive devices to remove";
            }

            return RedirectToAction("ManageDevices");
        }
        private static string GenerateSecureSixDigitCode()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var number = BitConverter.ToUInt32(bytes, 0) % 1000000;
            return number.ToString("D6");
        }
        public IActionResult Payments()
        {
            return View();
        }

        public IActionResult Orders()
        {
            return View();
        }

        public IActionResult ContactUs()
        {
            return View();
        }
    }
}
