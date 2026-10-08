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
    public class FollowUpsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public FollowUpsController(
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

        // GET: FollowUps
        public async Task<IActionResult> Index(string? statusFilter, DateTime? dateFilter, string? typeFilter)
        {
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(f => f.AssignedTo == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(f => f.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(typeFilter))
            {
                query = query.Where(f => f.FollowUpType == typeFilter);
            }

            if (dateFilter.HasValue)
            {
                var targetDate = dateFilter.Value.Date;
                query = query.Where(f => f.FollowUpDate.Date == targetDate);
            }

            var followUps = await query
                .OrderBy(f => f.FollowUpDate)
                .Select(f => new FollowUpListViewModel
                {
                    FollowUpId = f.FollowUpId,
                    FollowUpDate = f.FollowUpDate,
                    FollowUpType = f.FollowUpType,
                    Subject = f.Subject,
                    Status = f.Status,
                    Remarks = f.Remarks,
                    RelatedTo = f.Customer != null ? $"Customer: {f.Customer.CustomerName}"
                              : f.Lead != null ? $"Lead: {f.Lead.LeadName}"
                              : f.Opportunity != null ? $"Opportunity: {f.Opportunity.OpportunityName}"
                              : "General",
                    AssignedToName = f.AssignedToUser != null ? f.AssignedToUser.FullName : "Unassigned",
                    IsOverdue = f.FollowUpDate.Date < DateTime.UtcNow.Date && f.Status == FollowUpStatuses.Planned
                })
                .ToListAsync();

            ViewData["StatusFilter"] = statusFilter;
            ViewData["TypeFilter"] = typeFilter;
            ViewData["DateFilter"] = dateFilter?.ToString("yyyy-MM-dd");

            return View(followUps);
        }

        // GET: FollowUps/Create
        public async Task<IActionResult> Create(int? customerId, int? leadId, int? opportunityId)
        {
            await PopulateDropdownsAsync();

            var model = new CreateFollowUpViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                OpportunityId = opportunityId,
                FollowUpDate = DateTime.UtcNow.AddDays(1)
            };

            return View(model);
        }

        // POST: FollowUps/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateFollowUpViewModel model)
        {
            // Mandatory Business Validation (Section 5.3, 5.4, 17.7, 17.19):
            // "Follow-Up Date cannot be earlier than today for a new/planned follow-up."
            if (model.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
            }

            if (!FollowUpStatuses.All.Contains(FollowUpStatuses.Planned))
            {
                ModelState.AddModelError("Status", "Invalid status.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(model);
            }

            var assignedUserId = (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedTo))
                ? model.AssignedTo
                : CurrentUserId;

            var followUp = new FollowUp
            {
                Subject = model.Subject.Trim(),
                FollowUpDate = model.FollowUpDate,
                FollowUpType = model.FollowUpType,
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                OpportunityId = model.OpportunityId,
                Remarks = model.Remarks?.Trim(),
                Status = FollowUpStatuses.Planned,
                AssignedTo = assignedUserId,
                CreatedDate = DateTime.UtcNow
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "FollowUp",
                recordId: followUp.FollowUpId.ToString(),
                newValue: $"Subject={followUp.Subject}, Date={followUp.FollowUpDate:yyyy-MM-dd}, Type={followUp.FollowUpType}",
                details: $"Follow-up scheduled: '{followUp.Subject}' on {followUp.FollowUpDate:yyyy-MM-dd}.");

            TempData["SuccessMessage"] = $"Follow-up '{followUp.Subject}' scheduled successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FollowUps/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, string? completionRemarks)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!IsAdminOrManager && followUp.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            followUp.Status = FollowUpStatuses.Completed;
            if (!string.IsNullOrWhiteSpace(completionRemarks))
            {
                followUp.Remarks = (followUp.Remarks != null ? followUp.Remarks + " | Outcome: " : "Outcome: ") + completionRemarks;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Update,
                "FollowUp",
                recordId: id.ToString(),
                newValue: $"Status=Completed",
                details: $"Follow-up {followUp.Subject} marked as Completed.");

            TempData["SuccessMessage"] = "Follow-up marked as completed.";
            return RedirectToAction(nameof(Index));
        }

        // POST: FollowUps/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return NotFound();

            if (!IsAdminOrManager && followUp.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            _context.FollowUps.Remove(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Delete,
                "FollowUp",
                recordId: id.ToString(),
                oldValue: $"Subject={followUp.Subject}",
                details: $"Follow-up {followUp.Subject} deleted.");

            TempData["SuccessMessage"] = "Follow-up deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync()
        {
            var customers = await _context.Customers.OrderBy(c => c.CustomerName).ToListAsync();
            ViewBag.Customers = new SelectList(customers, "CustomerId", "CustomerName");

            var leads = await _context.Leads.Where(l => l.Status != LeadStatuses.Converted).OrderBy(l => l.LeadName).ToListAsync();
            ViewBag.Leads = new SelectList(leads, "LeadId", "LeadName");

            var opps = await _context.Opportunities.Where(o => o.Status == "Open").OrderBy(o => o.OpportunityName).ToListAsync();
            ViewBag.Opportunities = new SelectList(opps, "OpportunityId", "OpportunityName");

            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName");
        }
    }
}
