using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels;

namespace AcxiomCRM.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReturnUrl"] = model.ReturnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Find user by email or username
            var user = await _userManager.FindByEmailAsync(model.Email) 
                       ?? await _userManager.FindByNameAsync(model.Email);

            if (user == null)
            {
                await _auditService.LogAsync(
                    AuditActions.FailedLogin,
                    "Auth",
                    recordId: model.Email,
                    details: $"Failed login attempt for unknown user: {model.Email}",
                    result: "Failed");

                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync(
                    AuditActions.FailedLogin,
                    "Auth",
                    recordId: user.Id,
                    details: $"Login rejected for deactivated user: {user.UserName}",
                    result: "Failed",
                    explicitUserId: user.Id,
                    explicitUserName: user.UserName);

                ModelState.AddModelError(string.Empty, "This account has been deactivated. Please contact your system administrator.");
                return View(model);
            }

            // Perform sign in with lockout enabled per Section 6.3
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    AuditActions.Login,
                    "Auth",
                    recordId: user.Id,
                    details: $"User {user.UserName} successfully authenticated.",
                    result: "Success",
                    explicitUserId: user.Id,
                    explicitUserName: user.UserName);

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }

                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(
                    AuditActions.Lockout,
                    "Auth",
                    recordId: user.Id,
                    details: $"Account {user.UserName} locked due to repeated failed logins.",
                    result: "Failed",
                    explicitUserId: user.Id,
                    explicitUserName: user.UserName);

                return RedirectToAction(nameof(Lockout));
            }

            // Failed login
            await _auditService.LogAsync(
                AuditActions.FailedLogin,
                "Auth",
                recordId: user.Id,
                details: $"Invalid password attempt for {user.UserName}.",
                result: "Failed",
                explicitUserId: user.Id,
                explicitUserName: user.UserName);

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");
            }

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (createResult.Succeeded)
            {
                // Ensure assigned role is valid; fallback to SalesExecutive for security
                var role = AppRoles.All.Contains(model.Role) ? model.Role : AppRoles.SalesExecutive;
                await _userManager.AddToRoleAsync(user, role);

                await _auditService.LogAsync(
                    AuditActions.Create,
                    "User",
                    recordId: user.Id,
                    details: $"New user registered: {user.UserName} with role {role}.",
                    result: "Success",
                    explicitUserId: user.Id,
                    explicitUserName: user.UserName);

                TempData["SuccessMessage"] = "Registration successful! You may now sign in with your credentials.";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name;
            var userId = _userManager.GetUserId(User);

            await _signInManager.SignOutAsync();

            await _auditService.LogAsync(
                AuditActions.Logout,
                "Auth",
                recordId: userId,
                details: $"User {userName} signed out safely.",
                result: "Success",
                explicitUserId: userId,
                explicitUserName: userName);

            TempData["SuccessMessage"] = "You have been safely signed out.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Lockout()
        {
            return View();
        }
    }
}
