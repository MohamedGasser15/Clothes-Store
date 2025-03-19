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
using System.Security.Claims;

namespace Clothes_Store.Areas.Customer.Controllers
{
    [Area("Customer")]

    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender; 


        public AccountController(UserManager<ApplicationUser> userManager,
                                SignInManager<ApplicationUser> signInManager,
                                RoleManager<IdentityRole> roleManager, ApplicationDbContext db, IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
            _db = db;
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

                    await _emailSender.SendEmailAsync(user.Email, "Email Confirmation Code",
                        $"Your email confirmation code is: {verificationCode}");

                    return RedirectToAction("VerifyEmailCode", new { userId = user.Id });
                }

                AddErrors(result);
            }

            return View(model);
        }


        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string returnurl = null)
        {
            ViewData["ReturnUrl"] = returnurl;
            return View();
        }

        public async Task<IActionResult> Login(LoginViewModel model, string returnurl = null)
        {
            ViewData["ReturnUrl"] = returnurl;
            returnurl = returnurl ?? Url.Content("~/");
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);

                var result = await _signInManager.PasswordSignInAsync(user.Name, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return LocalRedirect(returnurl);
                }
                ModelState.AddModelError(string.Empty, "Invalid Login Attempt");
                return View(model);
            }
            return View(model);
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
                return View();
            }

            // Generate a 6-digit code
            var resetCode = new Random().Next(100000, 999999).ToString();

            // Store the code in a temporary place (e.g., user claims)
            await _userManager.RemoveClaimsAsync(user, await _userManager.GetClaimsAsync(user));
            await _userManager.AddClaimAsync(user, new Claim("ResetCode", resetCode));

            // Send email using SendGrid
            await _emailSender.SendEmailAsync(user.Email, "Password Reset Code",
                $"Your password reset code is: {resetCode}");

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

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmailCode(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return View("Error");
            }

            var model = new VerifyEmailCodeViewModel { UserId = userId };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyEmailCode(VerifyEmailCodeViewModel model)
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
                return RedirectToAction("Index", "Home");
            }

            return View("Error");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");

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
