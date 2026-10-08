using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels;

namespace AcxiomCRM.Web.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditService _auditService;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditService auditService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
        }

        // GET: Users
        public async Task<IActionResult> Index(string? search, string? roleFilter)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(term) || u.Email!.ToLower().Contains(term));
            }

            var users = await query.OrderByDescending(u => u.CreatedDate).ToListAsync();
            var userList = new List<UserListViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var userRole = roles.FirstOrDefault() ?? "None";

                if (!string.IsNullOrEmpty(roleFilter) && userRole != roleFilter)
                {
                    continue;
                }

                var isLockedOut = await _userManager.IsLockedOutAsync(user);

                userList.Add(new UserListViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber,
                    Role = userRole,
                    IsActive = user.IsActive,
                    IsLockedOut = isLockedOut,
                    CreatedDate = user.CreatedDate
                });
            }

            ViewData["Search"] = search;
            ViewData["RoleFilter"] = roleFilter;
            return View(userList);
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            return View(new CreateUserViewModel());
        }

        // POST: Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "A user with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                if (AppRoles.All.Contains(model.Role))
                {
                    await _userManager.AddToRoleAsync(user, model.Role);
                }

                await _auditService.LogAsync(
                    AuditActions.Create,
                    "User",
                    recordId: user.Id,
                    newValue: $"Name={user.FullName}, Email={user.Email}, Role={model.Role}",
                    details: $"Administrator created user account for {user.Email}.");

                TempData["SuccessMessage"] = $"User {user.FullName} created successfully with role {model.Role}.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                Role = roles.FirstOrDefault() ?? AppRoles.SalesExecutive,
                IsActive = user.IsActive
            };

            return View(model);
        }

        // POST: Users/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            var oldRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "None";
            var oldState = $"Name={user.FullName}, Active={user.IsActive}, Role={oldRole}";

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.IsActive = model.IsActive;

            var updateResult = await _userManager.UpdateAsync(user);
            if (updateResult.Succeeded)
            {
                // Update primary role if changed
                if (oldRole != model.Role && AppRoles.All.Contains(model.Role))
                {
                    if (!string.IsNullOrEmpty(oldRole) && oldRole != "None")
                    {
                        await _userManager.RemoveFromRoleAsync(user, oldRole);
                    }
                    await _userManager.AddToRoleAsync(user, model.Role);

                    await _auditService.LogAsync(
                        AuditActions.RoleChange,
                        "User",
                        recordId: user.Id,
                        oldValue: oldRole,
                        newValue: model.Role,
                        details: $"Role for user {user.UserName} updated from {oldRole} to {model.Role}.");
                }

                var newState = $"Name={user.FullName}, Active={user.IsActive}, Role={model.Role}";
                await _auditService.LogAsync(
                    AuditActions.Update,
                    "User",
                    recordId: user.Id,
                    oldValue: oldState,
                    newValue: newState,
                    details: $"Administrator updated user profile for {user.Email}.");

                TempData["SuccessMessage"] = $"User {user.FullName} updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // POST: Users/Unlock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            await _auditService.LogAsync(
                AuditActions.Security,
                "User",
                recordId: user.Id,
                details: $"Administrator explicitly unlocked account {user.UserName}.");

            TempData["SuccessMessage"] = $"Account {user.UserName} has been successfully unlocked.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Users/ToggleStatus/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            var actionName = user.IsActive ? "Activated" : "Deactivated";
            await _auditService.LogAsync(
                AuditActions.Update,
                "User",
                recordId: user.Id,
                newValue: $"IsActive={user.IsActive}",
                details: $"Administrator {actionName} user account {user.UserName}.");

            TempData["SuccessMessage"] = $"User {user.UserName} has been {actionName.ToLower()}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Users/ResetPassword/5
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(new ResetPasswordViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? user.UserName ?? string.Empty
            });
        }

        // POST: Users/ResetPassword/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(
                    AuditActions.Security,
                    "User",
                    recordId: user.Id,
                    details: $"Administrator reset password for user {user.UserName}. (No secrets logged per policy).");

                TempData["SuccessMessage"] = $"Password for {user.Email} was successfully reset.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }
    }
}
