using Clothes_DataAccess.Data;
using Clothes_Models.Models;
using Clothes_Models.ViewModels;
using Clothes_Store.Models.ViewModels;
using Clothes_Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]
    [AllowAnonymous]
    public class AccountController : BaseController
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        public AccountController(UserManager<ApplicationUser> userManager,
                                SignInManager<ApplicationUser> signInManager,
                                RoleManager<IdentityRole> roleManager,
                                ApplicationDbContext db,
                                IEmailSender emailSender) : base(db, userManager)
        {
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnurl = null)
        {
            ViewData["ReturnUrl"] = returnurl;
            return View();
        }

        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            returnUrl = returnUrl ?? Url.Content("~/");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            // User doesn't exist
            if (user == null)
            {
                TempData["ErrorMessage"] = "❌ Invalid email or password!";
                return View(model);
            }

            // Check password first
            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                TempData["ErrorMessage"] = "❌ Invalid email or password!";
                return View(model);
            }

            // Handle 2FA enabled users
            if (await _userManager.GetTwoFactorEnabledAsync(user))
            {
                // Generate and store 2FA code
                var code = new Random().Next(100000, 999999).ToString();
                var emailBody = GenerateEmail2FA(user, code);

                // Store in session with expiration
                HttpContext.Session.SetString("2FA_Code", code);
                HttpContext.Session.SetString("2FA_User", user.Id);
                HttpContext.Session.SetString("2FA_RememberMe", model.RememberMe.ToString());

                try
                {
                    await _emailSender.SendEmailAsync(
                        user.Email,
                        "Your Login Verification Code",
                        emailBody
                    );

                    return RedirectToAction("Enter2FACode", new { returnUrl });
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Failed to send verification email. Please try again.");
                    return View(model);
                }
            }

            // Regular login (no 2FA)
            await TrackUserDevice(user);
            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                HttpContext.Session.SetString("lang", user.PreferredLanguage ?? "en");
                return LocalRedirect(returnUrl);
            }

            // If we got here, something unexpected failed
            TempData["ErrorMessage"] = "❌ Login failed. Please try again.";
            return View(model);
        }
        private string GenerateEmail2FA(ApplicationUser user, string confirmationCode)
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
            
            <p>Alternatively, you can click the button below to verify your email:</p>
            
            <a href=""{Generate2FALink(user, confirmationCode)}"" class='button'>Verify Email Address</a>
            
            <p>If the button doesn't work, copy and paste this link into your browser:</p>
            <p style=""word-break: break-all;"">{Generate2FALink(user, confirmationCode)}</p>
        </div>
        
        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} Cara-Store. All rights reserved.</p>
            <p>This email was sent to {user.Email} as part of our verification process.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string Generate2FALink(ApplicationUser user, string code)
        {
            return Url.Action(
                "Enter2FACode",
                "Account",
                new { userId = user.Id, code = code },
                protocol: HttpContext.Request.Scheme
            );
        }
        [HttpGet]
        public IActionResult Enter2FACode() => View();

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Verify2FA(string code, bool rememberMe = false)
{
    // 1. Validate session data
    var userId = HttpContext.Session.GetString("2FA_User");
    var correctCode = HttpContext.Session.GetString("2FA_Code");
    var isExternalLogin = HttpContext.Session.GetString("2FA_Provider") != null;

    if (userId == null || correctCode == null)
    {
        TempData["ErrorMessage"] = "Session expired. Please login again.";
        return RedirectToAction("Login");
    }

    // 2. Verify code (with timing attack protection)
    if (!SecureEquals(code, correctCode))
    {
        TempData["ErrorMessage"] = "Invalid verification code";
        return View("Enter2FACode");
    }

    // 3. Get user and validate
    var user = await _userManager.FindByIdAsync(userId);
    if (user == null)
    {
        TempData["ErrorMessage"] = "User not found";
        return RedirectToAction("Login");
    }

    // 4. Handle external login completion if needed
    if (isExternalLogin)
    {
        var provider = HttpContext.Session.GetString("2FA_Provider");
        var info = await _signInManager.GetExternalLoginInfoAsync();
        
        if (info == null || info.LoginProvider != provider)
        {
            TempData["ErrorMessage"] = "External login validation failed";
            return RedirectToAction("Login");
        }

        // Complete the external auth flow
        await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
    }

    // 5. Final sign-in
    await _signInManager.SignInAsync(user, rememberMe);
    await TrackUserDevice(user);

    // 6. Cleanup session
    HttpContext.Session.Remove("2FA_Code");
    HttpContext.Session.Remove("2FA_User");
    HttpContext.Session.Remove("2FA_Provider");

    // 7. Redirect with success
    TempData["SuccessMessage"] = "Login successful!";
    return RedirectToAction("Index", "Home");
}

// Helper method to prevent timing attacks
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
        public async Task<IActionResult> Register(string returnurl = null)
        {

            ViewData["ReturnUrl"] = returnurl;
            RegisterViewModel registerViewModel = new();
            return View(registerViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string returnurl = null)
        {
            ViewData["ReturnUrl"] = returnurl ?? Url.Content("~/");

            if (ModelState.IsValid)
            {
                // Check if the email is already registered
                var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existingUserByEmail != null)
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    return View(model);
                }

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

                    var verificationCode = new Random().Next(100000, 999999).ToString();
                    await _userManager.AddClaimAsync(user, new Claim("EmailVerificationCode", verificationCode));

                    var emailBody = $@"
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
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 20px;
        }}
        .header h1 {{
            color: #4CAF50;
        }}
        .content {{
            text-align: center;
            margin-bottom: 20px;
        }}
        .content p {{
            font-size: 16px;
            color: #333333;
            line-height: 1.5;
        }}
        .verification-code {{
            font-size: 20px;
            font-weight: bold;
            color: #4CAF50;
            background-color: #f2f2f2;
            padding: 10px;
            border-radius: 6px;
            display: inline-block;
            margin: 20px 0;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #777;
            margin-top: 20px;
        }}
        .footer p {{
            margin: 0;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Email Verification</h1>
        </div>
        <div class='content'>
            <p>Hi {user.UserName},</p>
            <p>Thank you for registering with us! Please use the following verification code to complete your registration:</p>
            <p class='verification-code'>{verificationCode}</p>
            <p>Enter this code in the application to verify your email address.</p>
            <p>If you didn’t register, please ignore this email.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2025 Cara-Store. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

                    await _emailSender.SendEmailAsync(model.Email, "Email Confirmation Code", emailBody);

                    return RedirectToAction("VerifyEmailCode", new { userId = user.Id });
                }

                AddErrors(result);
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmailCode(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {

                return View("Error");
            }

            var model = new VerifyEmailViewModel { UserId = userId };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
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

            var claims = await _userManager.GetClaimsAsync(user);
            var storedCode = claims.FirstOrDefault(c => c.Type == "EmailVerificationCode")?.Value;

            if (storedCode == null || storedCode != model.Code)
            {
                ModelState.AddModelError("", "Invalid or expired code.");
                return View(model);
            }

            var result = await _userManager.ConfirmEmailAsync(user, await _userManager.GenerateEmailConfirmationTokenAsync(user));
            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false);
                await TrackUserDevice(user);
                return RedirectToAction("EmailConfirmationSuccess", "Account");
            }

            return View("Error");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResendEmailCode(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return RedirectToAction("Register");

            var claims = await _userManager.GetClaimsAsync(user);
            var lastRequestClaim = claims.FirstOrDefault(c => c.Type == "LastResendTime");
            DateTime lastRequestTime = lastRequestClaim != null ? DateTime.Parse(lastRequestClaim.Value) : DateTime.MinValue;

            if ((DateTime.UtcNow - lastRequestTime).TotalSeconds < 60)
            {
                TempData["ErrorMessage"] = "Please wait before requesting a new code.";
                return RedirectToAction("VerifyResetCode", new { email = user.Email });
            }

            var newCode = new Random().Next(100000, 999999).ToString();
            await _userManager.SetAuthenticationTokenAsync(user, "Default", "ResetCode", newCode);

            if (lastRequestClaim != null) await _userManager.RemoveClaimAsync(user, lastRequestClaim);
            await _userManager.AddClaimAsync(user, new Claim("LastResendTime", DateTime.UtcNow.ToString()));

            await _emailSender.SendEmailAsync(user.Email, "Email Reset Code", $"Your new code: {newCode}");

            TempData["Message"] = "A new code has been sent to your email.";
            return RedirectToAction("VerifyEmailCode", new { email = user.Email });
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult EmailConfirmationSuccess()
        {
            return View();
        }

        [HttpGet]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "No account found with this email.");
                return View(model);
            }

            // Generate a 6-digit code
            var resetCode = new Random().Next(100000, 999999).ToString();

            // Store the code in a temporary place (e.g., user claims)
            await _userManager.RemoveClaimsAsync(user, await _userManager.GetClaimsAsync(user));
            await _userManager.AddClaimAsync(user, new Claim("ResetCode", resetCode));
            var emailBody = $@"
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
            padding: 0;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            padding: 20px;
            border-radius: 8px;
            box-shadow: 0 4px 8px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 20px;
        }}
        .header h1 {{
            color: #4CAF50;
        }}
        .content {{
            text-align: center;
            margin-bottom: 20px;
        }}
        .content p {{
            font-size: 16px;
            color: #333333;
            line-height: 1.5;
        }}
        .reset-code {{
            font-size: 20px;
            font-weight: bold;
            color: #4CAF50;
            background-color: #f2f2f2;
            padding: 10px;
            border-radius: 6px;
            display: inline-block;
            margin: 20px 0;
        }}
        .footer {{
            text-align: center;
            font-size: 14px;
            color: #777;
            margin-top: 20px;
        }}
        .footer p {{
            margin: 0;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header'>
            <h1>Forgot Password</h1>
        </div>
        <div class='content'>
            <p>Hi {user.UserName},</p>
            <p>We received a request to reset your password. Please use the following code to reset your password:</p>
            <p class='reset-code'>{resetCode}</p>
            <p>If you didn't request this, please ignore this email. Your password will remain unchanged.</p>
        </div>
        <div class='footer'>
            <p>&copy; 2025 Cara-Store. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";

            // Send email using SendGrid
            await _emailSender.SendEmailAsync(user.Email, "Password Reset Code", emailBody);

            return RedirectToAction("VerifyResetCode", new { email = user.Email });
        }

        [HttpGet]
        public ActionResult VerifyResetCode(string email)
        {
            var model = new VerifyCodeViewModel { Email = email };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> VerifyResetCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid request.");
                return View();
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var storedCode = claims.FirstOrDefault(c => c.Type == "ResetCode")?.Value;

            if (storedCode == null || storedCode != model.Code)
            {
                ModelState.AddModelError("", "Invalid or expired code.");
                return View();
            }

            return RedirectToAction("ResetPassword", new { email = model.Email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResendCode(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return RedirectToAction("ForgotPassword");

            var claims = await _userManager.GetClaimsAsync(user);
            var lastRequestClaim = claims.FirstOrDefault(c => c.Type == "LastResendTime");
            DateTime lastRequestTime = lastRequestClaim != null ? DateTime.Parse(lastRequestClaim.Value) : DateTime.MinValue;

            if ((DateTime.UtcNow - lastRequestTime).TotalSeconds < 60)
            {
                TempData["ErrorMessage"] = "Please wait before requesting a new code.";
                return RedirectToAction("VerifyResetCode", new { email = user.Email });
            }

            var newCode = new Random().Next(100000, 999999).ToString();
            await _userManager.SetAuthenticationTokenAsync(user, "Default", "ResetCode", newCode);

            if (lastRequestClaim != null) await _userManager.RemoveClaimAsync(user, lastRequestClaim);
            await _userManager.AddClaimAsync(user, new Claim("LastResendTime", DateTime.UtcNow.ToString()));

            await _emailSender.SendEmailAsync(user.Email, "Password Reset Code", $"Your new code: {newCode}");

            TempData["Message"] = "A new code has been sent to your email.";
            return RedirectToAction("VerifyResetCode", new { email = user.Email });
        }


        [HttpGet]
        public ActionResult ResetPassword(string email)
        {
            var model = new ResetPasswordViewModel
            {
                Email = email
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
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

            var result = await _userManager.RemovePasswordAsync(user);
            if (result.Succeeded)
            {
                result = await _userManager.AddPasswordAsync(user, model.NewPassword);
                if (result.Succeeded)
                {
                    return RedirectToAction("ResetPasswordConfirmation");
                }
            }
            AddErrors(result);
            return View();
        }
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }

        

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult ExternalLogin(string provider, string returnurl = null)
        {
            var redirectUrl = Url.Action("ExternalLoginCallback", "Account", new { returnurl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return Challenge(properties, provider);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalLoginCallback(string returnurl = null, string remoteError = null)
        {
            returnurl = returnurl ?? Url.Content("~/");
            if (remoteError != null)
            {
                TempData["ErrorMessage"] = $"Error from {remoteError} provider";
                return RedirectToAction(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                Console.WriteLine("External login info is null");
                TempData["ErrorMessage"] = "Unable to retrieve login information from the provider.";
                return RedirectToAction(nameof(Login));
            }

            // Check if user exists
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            if (user == null)
            {
                // New user flow
                ViewData["ReturnUrl"] = returnurl;
                ViewData["ProviderDisplayName"] = info.ProviderDisplayName;
                var email = info.Principal.FindFirstValue(ClaimTypes.Email);
                return RedirectToAction("ExternalLoginConfirmation", new { returnurl, email });
            }

            // Existing user - sign in directly
            var result = await _signInManager.ExternalLoginSignInAsync(
                info.LoginProvider,
                info.ProviderKey,
                isPersistent: false
            );

            if (result.Succeeded)
            {
                await TrackUserDevice(user);
                await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
                return LocalRedirect(returnurl);
            }

            TempData["ErrorMessage"] = "Login failed";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ExternalLoginConfirmation(string returnurl, string email)
        {
            returnurl = returnurl ?? Url.Content("~/");

            return View(new ExternalLoginConfirmationViewModel
            {
                Email = email
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExternalLoginConfirmation(ExternalLoginConfirmationViewModel model, string returnurl = null)
        {
            returnurl = returnurl ?? Url.Content("~/");

            // Retrieve external login info
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return View("Error");
            }

            // Check if the email already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "Email already registered");
                ViewData["ReturnUrl"] = returnurl;
                return View(model);
            }

            // Create the new user
            var user = new ApplicationUser
            {
                Name = model.Name,
                Email = model.Email,
                UserName = model.Email // Ensure UserName is set as required by your app
            };

            var result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, SD.User); // Assign user role
                result = await _userManager.AddLoginAsync(user, info); // Associate external login
                if (result.Succeeded)
                {
                    await TrackUserDevice(user); // Custom method (assumed)
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    await _signInManager.UpdateExternalAuthenticationTokensAsync(info);
                    return LocalRedirect(returnurl);
                }
            }

            // Handle any errors during user creation or login association
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewData["ReturnUrl"] = returnurl;
            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Home", "Home");

        }
        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}
