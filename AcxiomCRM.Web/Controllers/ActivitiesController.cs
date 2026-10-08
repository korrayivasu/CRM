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
    public class ActivitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public ActivitiesController(
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

        // GET: Activities
        public async Task<IActionResult> Index(string? typeFilter, string? statusFilter, DateTime? dateFilter)
        {
            var query = _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.Opportunity)
                .Include(a => a.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(a => a.AssignedTo == CurrentUserId);
            }

            // Search/Filter by Activity Type, Date, Status, Assigned User (Section 17.13)
            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                query = query.Where(a => a.ActivityType == typeFilter);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(a => a.Status == statusFilter);
            }

            if (dateFilter.HasValue)
            {
                var target = dateFilter.Value.Date;
                query = query.Where(a => a.ActivityDate.Date == target);
            }

            var activities = await query
                .OrderByDescending(a => a.ActivityDate)
                .Select(a => new ActivityListViewModel
                {
                    ActivityId = a.ActivityId,
                    ActivityType = a.ActivityType,
                    Subject = a.Subject,
                    Description = a.Description,
                    ActivityDate = a.ActivityDate,
                    Status = a.Status,
                    RelatedTo = a.Customer != null ? $"Customer: {a.Customer.CustomerName}"
                              : a.Lead != null ? $"Lead: {a.Lead.LeadName}"
                              : a.Opportunity != null ? $"Opportunity: {a.Opportunity.OpportunityName}"
                              : "General",
                    AssignedToName = a.AssignedToUser != null ? a.AssignedToUser.FullName : "Unassigned"
                })
                .ToListAsync();

            ViewData["TypeFilter"] = typeFilter;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["DateFilter"] = dateFilter?.ToString("yyyy-MM-dd");

            return View(activities);
        }

        // GET: Activities/Create
        public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
        {
            await PopulateDropdownsAsync();

            var model = new CreateActivityViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                OpportunityId = opportunityId,
                ActivityDate = DateTime.UtcNow
            };

            return View(model);
        }

        // POST: Activities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateActivityViewModel model)
        {
            if (!ActivityTypes.All.Contains(model.ActivityType))
            {
                ModelState.AddModelError("ActivityType", "Invalid Activity Type.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(model);
            }

            var assignedUserId = (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedTo))
                ? model.AssignedTo
                : CurrentUserId;

            var activity = new Activity
            {
                ActivityType = model.ActivityType,
                Subject = model.Subject.Trim(),
                Description = model.Description?.Trim(),
                ActivityDate = model.ActivityDate,
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                OpportunityId = model.OpportunityId,
                Status = model.Status,
                AssignedTo = assignedUserId,
                CreatedDate = DateTime.UtcNow
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "Activity",
                recordId: activity.ActivityId.ToString(),
                newValue: $"Type={activity.ActivityType}, Subject={activity.Subject}",
                details: $"{activity.ActivityType} logged: '{activity.Subject}'.");

            TempData["SuccessMessage"] = $"{activity.ActivityType} activity logged successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Activities/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var activity = await _context.Activities.FindAsync(id);
            if (activity == null) return NotFound();

            if (!IsAdminOrManager && activity.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Delete,
                "Activity",
                recordId: id.ToString(),
                oldValue: $"Subject={activity.Subject}",
                details: $"Activity {activity.Subject} deleted.");

            TempData["SuccessMessage"] = "Activity deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync()
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName");

            var leads = await _context.Leads.Where(l => l.Status != LeadStatuses.Converted).OrderBy(l => l.LeadName).ToListAsync();
            ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName");

            var opps = await _context.Opportunities.OrderBy(o => o.OpportunityName).ToListAsync();
            ViewBag.Opportunities = new SelectList(opps, "OpportunityId", "OpportunityName");

            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName");
        }
    }
}
