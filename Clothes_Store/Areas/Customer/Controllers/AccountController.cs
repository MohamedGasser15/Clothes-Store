using System;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Store.Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    [AllowAnonymous]
    public class AccountController : BaseController
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db,
            IEmailSender emailSender) : base(db, userManager)
        {
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string returnurl = null)
        {
            ViewData["ReturnUrl"] = returnurl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");

            // Validate user input
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Invalid email or password.";
                return View(model);
            }

            // Verify password
            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                TempData["ErrorMessage"] = "Invalid email or password.";
                return View(model);
            }

            // Handle 2FA if enabled
            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                var code = new Random().Next(100000, 999999).ToString();
                var emailBody = GenerateEmail2FA(user, code);

                HttpContext.Session.SetString("2FA_Code", code);
                HttpContext.Session.SetString("2FA_User", user.Id);
                HttpContext.Session.SetString("2FA_RememberMe", model.RememberMe.ToString());

                try
                {
                    await _emailSender.SendEmailAsync(user.Email, "Login Verification Code", emailBody);
                    return RedirectToAction("Enter2FACode", new { returnUrl });
                }
                catch
                {
                    ModelState.AddModelError(string.Empty, "Failed to send verification email.");
                    return View(model);
                }
            }

            // Perform regular login
            await TrackUserDevice(user);
            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                HttpContext.Session.SetString("lang", user.PreferredLanguage ?? "en");
                return LocalRedirect(returnUrl);
            }

            TempData["ErrorMessage"] = "Login failed.";
            return View(model);
        }

        // GET: /Account/Enter2FACode
        [HttpGet]
        public IActionResult Enter2FACode()
        {
            return View();
        }

        // POST: /Account/Verify2FA
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(string code, bool rememberMe = false)
        {
            // Validate session data
            var userId = HttpContext.Session.GetString("2FA_User");
            var correctCode = HttpContext.Session.GetString("2FA_Code");
            var isExternalLogin = HttpContext.Session.GetString("2FA_Provider") != null;

            if (userId == null || correctCode == null)
            {
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToAction("Login");
            }

            // Verify code securely
            if (!SecureEquals(code, correctCode))
            {
                TempData["ErrorMessage"] = "Invalid verification code.";
                return View("Enter2FACode");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Handle external login
            if (isExternalLogin)
            {
                var provider = HttpContext.Session.GetString("2FA_Provider");
                var info = await _signInManager.GetExternalLoginInfoAsync();
                if (info == null || info.LoginProvider != provider)
                {
                    TempData["ErrorMessage"] = "External login validation failed.";
                    return RedirectToAction("Login");
                }

                await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
            }

            // Sign in user and clean up session
            await _signInManager.SignInAsync(user, rememberMe);
            await TrackUserDevice(user);

            HttpContext.Session.Remove("2FA_Code");
            HttpContext.Session.Remove("2FA_User");
            HttpContext.Session.Remove("2FA_Provider");

            TempData["SuccessMessage"] = "Login successful!";
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register(string returnurl = null)
        {
            ViewData["ReturnUrl"] = returnurl;
            return View(new RegisterViewModel { ReturnUrl = returnurl });
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnurl = null)
        {
            returnurl = returnurl ?? Url.Content("~/");

            // Validate user input
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check email and username availability
            var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
            if (existingUserByEmail != null)
            {
                ModelState.AddModelError("Email", "This email is already registered.");
                return View(model);
            }

            var existingUserByName = await _userManager.FindByNameAsync(model.Name);
            if (existingUserByName != null)
            {
                ModelState.AddModelError("Name", "This name is already taken.");
                return View(model);
            }

            // Validate name format
            if (string.IsNullOrWhiteSpace(model.Name) || model.Name.Length < 3)
            {
                ModelState.AddModelError("Name", "Name must be at least 3 characters long.");
                return View(model);
            }

            if (!Regex.IsMatch(model.Name, @"^[a-zA-Z\s]+$"))
            {
                ModelState.AddModelError("Name", "Name can only contain letters and spaces.");
                return View(model);
            }

            // Create new user
            var user = new ApplicationUser
            {
                UserName = model.Name,
                Email = model.Email,
                Name = model.Name
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, SD.User);

                // Send verification email
                var verificationCode = new Random().Next(100000, 999999).ToString();
                await _userManager.AddClaimAsync(user, new Claim("EmailVerificationCode", verificationCode));

                var emailBody = GenerateVerificationEmail(user, verificationCode);
                try
                {
                    await _emailSender.SendEmailAsync(model.Email, "Email Confirmation Code", emailBody);
                    return RedirectToAction("VerifyEmailCode", new { userId = user.Id });
                }
                catch
                {
                    ModelState.AddModelError(string.Empty, "Failed to send verification email.");
                    return View(model);
                }
            }

            AddErrors(result);
            return View(model);
        }

        // GET: /Account/VerifyEmailCode
        [HttpGet]
        public async Task<IActionResult> VerifyEmailCode(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View("Error");
            }

            return View(new VerifyEmailViewModel { UserId = userId });
        }

        // POST: /Account/VerifyEmailCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailCode(VerifyEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return View("Error");
            }

            // Verify email code
            var claims = await _userManager.GetClaimsAsync(user);
            var storedCode = claims.FirstOrDefault(c => c.Type == "EmailVerificationCode")?.Value;

            if (storedCode == null || storedCode != model.Code)
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired code.");
                return View(model);
            }

            // Confirm email and sign in
            var result = await _userManager.ConfirmEmailAsync(user, await _userManager.GenerateEmailConfirmationTokenAsync(user));
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                await TrackUserDevice(user);
                return RedirectToAction("EmailConfirmationSuccess");
            }

            return View("Error");
        }

        // POST: /Account/ResendEmailCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmailCode(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return RedirectToAction("Register");
            }

            // Enforce 60-second cooldown
            var claims = await _userManager.GetClaimsAsync(user);
            var lastRequestClaim = claims.FirstOrDefault(c => c.Type == "LastResendTime");
            var lastRequestTime = lastRequestClaim != null ? DateTime.Parse(lastRequestClaim.Value) : DateTime.MinValue;

            if ((DateTime.UtcNow - lastRequestTime).TotalSeconds < 60)
            {
                TempData["ErrorMessage"] = "Please wait before requesting a new code.";
                return RedirectToAction("VerifyEmailCode", new { email });
            }

            // Send new verification code
            var newCode = new Random().Next(100000, 999999).ToString();
            await _userManager.AddClaimAsync(user, new Claim("EmailVerificationCode", newCode));

            if (lastRequestClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, lastRequestClaim);
            }
            await _userManager.AddClaimAsync(user, new Claim("LastResendTime", DateTime.UtcNow.ToString()));

            var emailBody = GenerateVerificationEmail(user, newCode);
            await _emailSender.SendEmailAsync(user.Email, "Email Confirmation Code", emailBody);

            TempData["Message"] = "A new code has been sent to your email.";
            return RedirectToAction("VerifyEmailCode", new { email });
        }

        // GET: /Account/EmailConfirmationSuccess
        [HttpGet]
        public IActionResult EmailConfirmationSuccess()
        {
            return View();
        }

        // GET: /Account/ForgotPassword
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // POST: /Account/ForgotPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "No account found with this email.");
                return View(model);
            }
            var ipAddress = GetClientIpAddress();
            var deviceName = System.Net.Dns.GetHostName();
            var changeTime = DateTime.UtcNow;
            // Send password reset code
            var resetCode = new Random().Next(100000, 999999).ToString();
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = passwordResetLink(user, resetToken);

            await _userManager.RemoveClaimsAsync(user, await _userManager.GetClaimsAsync(user));
            await _userManager.AddClaimAsync(user, new Claim("ResetCode", resetCode));

            var emailBody = GenerateForgotPasswordEmail(user, ipAddress, deviceName, changeTime, resetCode, resetLink);
            try
            {
                await _emailSender.SendEmailAsync(user.Email, "Password Reset Code", emailBody);
                return RedirectToAction("VerifyResetCode", new { email = user.Email });
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Failed to send reset email.");
                return View(model);
            }
        }

        // GET: /Account/VerifyResetCode
        [HttpGet]
        public IActionResult VerifyResetCode(string email)
        {
            return View(new VerifyCodeViewModel { Email = email });
        }

        // POST: /Account/VerifyResetCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyResetCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid request.");
                return View(model);
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var storedCode = claims.FirstOrDefault(c => c.Type == "ResetCode")?.Value;

            if (storedCode == null || storedCode != model.Code)
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired code.");
                return View(model);
            }

            return RedirectToAction("ResetPassword", new { email = model.Email });
        }

        // POST: /Account/ResendCode
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendCode(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return RedirectToAction("ForgotPassword");
            }

            // Enforce 60-second cooldown
            var claims = await _userManager.GetClaimsAsync(user);
            var lastRequestClaim = claims.FirstOrDefault(c => c.Type == "LastResendTime");
            var lastRequestTime = lastRequestClaim != null ? DateTime.Parse(lastRequestClaim.Value) : DateTime.MinValue;

            if ((DateTime.UtcNow - lastRequestTime).TotalSeconds < 60)
            {
                TempData["ErrorMessage"] = "Please wait before requesting a new code.";
                return RedirectToAction("VerifyResetCode", new { email });
            }

            // Send new reset code
            var newCode = new Random().Next(100000, 999999).ToString();
            await _userManager.AddClaimAsync(user, new Claim("ResetCode", newCode));

            if (lastRequestClaim != null)
            {
                await _userManager.RemoveClaimAsync(user, lastRequestClaim);
            }
            await _userManager.AddClaimAsync(user, new Claim("LastResendTime", DateTime.UtcNow.ToString()));

            var emailBody = GenerateVerificationEmail(user, newCode);
            await _emailSender.SendEmailAsync(user.Email, "Password Reset Code", emailBody);

            TempData["Message"] = "A new code has been sent to your email.";
            return RedirectToAction("VerifyResetCode", new { email });
        }

        // GET: /Account/ResetPassword
        [HttpGet]
        public IActionResult ResetPassword(string email)
        {
            return View(new ResetPasswordViewModel { Email = email });
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return RedirectToAction("ResetPasswordConfirmation");
            }

            // Reset user password
            var result = await _userManager.RemovePasswordAsync(user);
            if (result.Succeeded)
            {
                var activity = new SecurityActivity
                {
                    UserId = user.Id,
                    ActivityType = "PasswordChange",
                    Description = "Password changed",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                };
                _db.SecurityActivities.Add(activity);
                await _db.SaveChangesAsync();

                result = await _userManager.AddPasswordAsync(user, model.NewPassword);
                if (result.Succeeded)
                {
                    return RedirectToAction("ResetPasswordConfirmation");
                }
            }

            AddErrors(result);
            return View(model);
        }

        // GET: /Account/ResetPasswordConfirmation
        [HttpGet]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        // POST: /Account/ExternalLogin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnurl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnurl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        // GET: /Account/ExternalLoginCallback
        [HttpGet]
        public async Task<IActionResult> ExternalLoginCallback(string returnurl = null, string remoteError = null)
        {
            returnurl = returnurl ?? Url.Content("~/");

            if (remoteError != null)
            {
                TempData["ErrorMessage"] = $"Error from {remoteError} provider.";
                return RedirectToAction("Login");
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["ErrorMessage"] = "Unable to retrieve login information.";
                return RedirectToAction("Login");
            }

            // Sign in existing user
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user != null)
            {
                var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);
                if (result.Succeeded)
                {
                    await TrackUserDevice(user);
                    await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
                    return LocalRedirect(returnurl);
                }
            }

            // Redirect to confirmation for new user
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            return RedirectToAction("ExternalLoginConfirmation", new { returnurl, email });
        }

        // GET: /Account/ExternalLoginConfirmation
        [HttpGet]
        public IActionResult ExternalLoginConfirmation(string returnurl, string email)
        {
            returnurl = returnurl ?? Url.Content("~/");
            return View(new ExternalLoginConfirmationViewModel { Email = email });
        }

        // POST: /Account/ExternalLoginConfirmation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnurl = null)
        {
            returnurl = returnurl ?? Url.Content("~/");

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return View("Error");
            }

            // Check email availability
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "Email already registered.");
                ViewData["ReturnUrl"] = returnurl;
                return View(model);
            }

            // Create new user
            var user = new ApplicationUser
            {
                Name = model.Name,
                Email = model.Email,
                UserName = model.Email
            };

            var result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, SD.User);
                result = await _userManager.AddLoginAsync(user, info);
                if (result.Succeeded)
                {
                    await TrackUserDevice(user);
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
                    return LocalRedirect(returnurl);
                }
            }

            AddErrors(result);
            ViewData["ReturnUrl"] = returnurl;
            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Home", "Home");
        }

        private string GenerateEmail2FA(ApplicationUser user, string code)
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
        .verification-code {{
            font-size: 28px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .security-alert {{
            background-color: #f8f9fa;
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
                {code}
            </div>
            
            <p>This code will expire in 15 minutes. If you didn't request this, please ignore this email.</p>
            
            <div class='security-alert'>
                <p><strong>Security Tip:</strong> Never share this code with anyone. Trendsvalley will never ask for your verification code.</p>
            </div>
            
            <p>Alternatively, you can click the button below to verify your email:</p>
            
            <a href=""{Generate2FALink(user, code)}"" class='button'>Verify Email Address</a>
            
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{Generate2FALink(user, code)}</p>
        </div>
        
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our verification process.</p>
        </div>
    </div>
</body>
</html>";
        }
        private string GenerateVerificationEmail(ApplicationUser user, string code)
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
        .verification-code {{
            font-size: 28px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 25px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .security-alert {{
            background-color: #f8f9fa;
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
            
            <p>Thank you for registering with LushThreads! Please use the following verification code to confirm your email address:</p>
            
            <div class='verification-code'>
                {code}
            </div>
            
            <p>This code will expire in 15 minutes. If you didn't request this, please ignore this email.</p>
            
            <div class='security-alert'>
                <p><strong>Security Tip:</strong> Never share this code with anyone. LushThreads will never ask for your verification code.</p>
            </div>
            
            <p>Alternatively, you can click the button below to verify your email:</p>
            
            <a href=""{GenerateVerificationLink(user, code)}"" class='button'>Verify Email Address</a>
            
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{GenerateVerificationLink(user, code)}</p>
        </div>
        
        <div class='footer'>
            <p>© {DateTime.Now.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our verification process.</p>
        </div>
    </div>
</body>
</html>";
        }
        private string GenerateForgotPasswordEmail(
    ApplicationUser user,
    string ipAddress,
    string deviceName,
    DateTime requestTime,
    string code,
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
        .verification-code {{
            font-size: 24px;
            font-weight: bold;
            color: #088178;
            letter-spacing: 3px;
            text-align: center;
            margin: 20px 0;
            padding: 15px;
            background-color: #f8f9fa;
            border-radius: 6px;
            border: 1px dashed #088178;
        }}
        .security-alert {{
            background-color: #f8f9fa;
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
            padding: 10px 20px;
            background-color: #088178;
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
        .security-message {{
            color: #088178;
            font-weight: bold;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Password Reset Verification</h1>
        </div>
        
        <div class='content'>
            <p>Hello {user.Name},</p>
            
            <p class='security-message'>A password reset was requested for your account!</p>
            
            <div class='security-alert'>
                <p><strong>SECURITY NOTICE:</strong> Use this verification code to confirm your identity:</p>
                <div class='verification-code'>
                    {code}
                </div>
                <p>This code expires in 15 minutes.</p>
            </div>
            
            <div class='security-alert'>
                <p><strong>IMPORTANT:</strong> Do not share this code with anyone. LushThreads will never ask for your verification code.</p>
            </div>
            
            <p>Request details:</p>
            <div class='info-item'>
                <span class='info-label'>Device:</span> {deviceName}
            </div>
            <div class='info-item'>
                <span class='info-label'>IP Address:</span> {ipAddress}
            </div>
            <div class='info-item'>
                <span class='info-label'>Time:</span> {requestTime:f}
            </div>
            
            <p>To complete your password reset, click the button below:</p>
            
            <a href=""{passwordResetLink}"" class='button'>Reset Password</a>
            
            <p style=""word-break: break-all;"">Or copy this link manually: {passwordResetLink}</p>
            
            <p>If you didn't request this password reset, please secure your account immediately.</p>
        </div>
        
        <div class='footer'>
            <p>© {requestTime.Year} LushThreads. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our security notifications.</p>
        </div>
    </div>
</body>
</html>";
        }
        private string Generate2FALink(ApplicationUser user, string code)
        {
            return Url.Action("Enter2FACode", "Account", new { userId = user.Id, code }, protocol: HttpContext.Request.Scheme);
        }

        private string GenerateVerificationLink(ApplicationUser user, string code)
        {
            return Url.Action(
                "VerifyEmailCode",
                "Account",
                new { userId = user.Id, code = code },
                protocol: HttpContext.Request.Scheme
            );
        }
        private string passwordResetLink(ApplicationUser user, string code)
        {
            return Url.Action(
                "ResetPassword",
                "Account",
                new { userId = user.Id, code = code },
                protocol: HttpContext.Request.Scheme
            );
        }
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private bool SecureEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            var result = 0;
            for (int i = 0; i < a.Length; i++)
            {
                result |= a[i] ^ b[i];
            }
            return result == 0;
        }
    }
}