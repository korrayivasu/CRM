using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.Services;
using AcxiomCRM.Web.ViewModels;

namespace AcxiomCRM.Web.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public CustomersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        // GET: Customers
        public async Task<IActionResult> Index(string? search, string? statusFilter)
        {
            var query = _context.Customers
                .Include(c => c.CreatedByUser)
                .Include(c => c.Opportunities)
                .AsQueryable();

            // Enforce role-based access scoping (Section 7, 17.10)
            if (!IsAdminOrManager)
            {
                query = query.Where(c => c.CreatedBy == CurrentUserId);
            }

            // Search filter by Name, Email, Phone, Company (Section 17.13)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c => c.CustomerName.ToLower().Contains(term)
                                      || c.Email.ToLower().Contains(term)
                                      || c.Phone.Contains(term)
                                      || (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(c => c.Status == statusFilter);
            }

            var customers = await query
                .OrderByDescending(c => c.CreatedDate)
                .Select(c => new CustomerListViewModel
                {
                    CustomerId = c.CustomerId,
                    CustomerCode = c.CustomerCode,
                    CustomerName = c.CustomerName,
                    Email = c.Email,
                    Phone = c.Phone,
                    CompanyName = c.CompanyName,
                    City = c.City,
                    State = c.State,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    CreatedByName = c.CreatedByUser != null ? c.CreatedByUser.FullName : "System",
                    OpportunityCount = c.Opportunities.Count
                })
                .ToListAsync();

            ViewData["Search"] = search;
            ViewData["StatusFilter"] = statusFilter;
            return View(customers);
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers
                .Include(c => c.CreatedByUser)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound();

            // Authorization check
            if (!IsAdminOrManager && customer.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            var opportunities = await _context.Opportunities
                .Where(o => o.CustomerId == customer.CustomerId)
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            var leads = await _context.Leads
                .Where(l => l.ConvertedCustomerId == customer.CustomerId)
                .ToListAsync();

            var followUps = await _context.FollowUps
                .Include(f => f.AssignedToUser)
                .Where(f => f.CustomerId == customer.CustomerId)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            var activities = await _context.Activities
                .Include(a => a.AssignedToUser)
                .Where(a => a.CustomerId == customer.CustomerId)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            var model = new CustomerDetailsViewModel
            {
                Customer = customer,
                Opportunities = opportunities,
                Leads = leads,
                FollowUps = followUps,
                Activities = activities
            };

            return View(model);
        }

        // GET: Customers/Create
        public async Task<IActionResult> Create()
        {
            await PopulateUserDropdownAsync();
            return View(new CreateCustomerViewModel());
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCustomerViewModel model)
        {
            // Server-side duplicate validation (Section 5.2, 17.5, 17.7)
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "A customer with this email address already exists.");
            }

            if (await _context.Customers.AnyAsync(c => c.Phone == model.Phone))
            {
                ModelState.AddModelError("Phone", "A customer with this phone number already exists.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateUserDropdownAsync();
                return View(model);
            }

            // Generate unique CustomerCode (e.g. CUST-10004)
            var lastCustomer = await _context.Customers.OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
            var nextNumber = (lastCustomer != null ? lastCustomer.CustomerId + 10001 : 10001);
            var customerCode = $"CUST-{nextNumber}";

            var ownerId = (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedOwnerId)) 
                ? model.AssignedOwnerId 
                : CurrentUserId;

            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = model.CustomerName.Trim(),
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Address = model.Address?.Trim(),
                City = model.City?.Trim(),
                State = model.State?.Trim(),
                Status = model.Status,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = ownerId
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Audit Log
            await _auditService.LogAsync(
                AuditActions.Create,
                "Customer",
                recordId: customer.CustomerId.ToString(),
                newValue: $"Code={customer.CustomerCode}, Name={customer.CustomerName}, Email={customer.Email}, Status={customer.Status}",
                details: $"Customer {customer.CustomerName} ({customer.CustomerCode}) created.");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' created successfully with code {customer.CustomerCode}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return NotFound();

            if (!IsAdminOrManager && customer.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            await PopulateUserDropdownAsync();

            var model = new EditCustomerViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                AssignedOwnerId = customer.CreatedBy
            };

            return View(model);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditCustomerViewModel model)
        {
            var customer = await _context.Customers.FindAsync(model.CustomerId);
            if (customer == null) return NotFound();

            if (!IsAdminOrManager && customer.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            // Server-side uniqueness check excluding self
            if (await _context.Customers.AnyAsync(c => c.CustomerId != model.CustomerId && c.Email.ToLower() == model.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Another customer already uses this email address.");
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != model.CustomerId && c.Phone == model.Phone))
            {
                ModelState.AddModelError("Phone", "Another customer already uses this phone number.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateUserDropdownAsync();
                return View(model);
            }

            var oldSnapshot = $"Name={customer.CustomerName}, Email={customer.Email}, Phone={customer.Phone}, Status={customer.Status}";

            customer.CustomerName = model.CustomerName.Trim();
            customer.Email = model.Email.Trim().ToLower();
            customer.Phone = model.Phone.Trim();
            customer.CompanyName = model.CompanyName?.Trim();
            customer.Address = model.Address?.Trim();
            customer.City = model.City?.Trim();
            customer.State = model.State?.Trim();
            customer.Status = model.Status;
            customer.ModifiedDate = DateTime.UtcNow;

            if (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedOwnerId))
            {
                customer.CreatedBy = model.AssignedOwnerId;
            }

            await _context.SaveChangesAsync();

            var newSnapshot = $"Name={customer.CustomerName}, Email={customer.Email}, Phone={customer.Phone}, Status={customer.Status}";

            await _auditService.LogAsync(
                AuditActions.Update,
                "Customer",
                recordId: customer.CustomerId.ToString(),
                oldValue: oldSnapshot,
                newValue: newSnapshot,
                details: $"Customer {customer.CustomerCode} updated.");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Customers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await _context.Customers
                .Include(c => c.Opportunities)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return NotFound();

            if (!IsAdminOrManager && customer.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            var oldSnapshot = $"Code={customer.CustomerCode}, Name={customer.CustomerName}";

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Delete,
                "Customer",
                recordId: id.ToString(),
                oldValue: oldSnapshot,
                details: $"Customer {customer.CustomerName} ({customer.CustomerCode}) deleted.");

            TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' was successfully deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateUserDropdownAsync()
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName");
        }
    }
}
