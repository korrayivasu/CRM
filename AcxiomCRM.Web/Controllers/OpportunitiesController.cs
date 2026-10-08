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
    public class OpportunitiesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public OpportunitiesController(
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

        // GET: Opportunities
        public async Task<IActionResult> Index(string? search, string? stageFilter, string? statusFilter, string? viewMode = "table")
        {
            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(o => o.AssignedTo == CurrentUserId);
            }

            // Search by Opportunity Name, Customer Name, Stage, Status (Section 17.13)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(o => o.OpportunityName.ToLower().Contains(term)
                                      || (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term))
                                      || (o.Customer != null && o.Customer.CustomerCode.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(stageFilter))
            {
                query = query.Where(o => o.Stage == stageFilter);
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(o => o.Status == statusFilter);
            }

            var opps = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();

            ViewData["Search"] = search;
            ViewData["StageFilter"] = stageFilter;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["ViewMode"] = viewMode;

            if (viewMode == "kanban")
            {
                var kanban = new PipelineKanbanViewModel
                {
                    Qualification = opps.Where(o => o.Stage == OpportunityStages.Qualification).ToList(),
                    Proposal = opps.Where(o => o.Stage == OpportunityStages.Proposal).ToList(),
                    Negotiation = opps.Where(o => o.Stage == OpportunityStages.Negotiation).ToList(),
                    Won = opps.Where(o => o.Stage == OpportunityStages.Won).ToList(),
                    Lost = opps.Where(o => o.Stage == OpportunityStages.Lost).ToList(),
                    TotalPipelineValue = opps.Where(o => o.Status == "Open").Sum(o => o.Amount),
                    TotalWeightedValue = opps.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount)
                };
                return View("Kanban", kanban);
            }

            var list = opps.Select(o => new OpportunityListViewModel
            {
                OpportunityId = o.OpportunityId,
                OpportunityName = o.OpportunityName,
                CustomerId = o.CustomerId,
                CustomerName = o.Customer?.CustomerName ?? "Unknown",
                CustomerCode = o.Customer?.CustomerCode ?? "",
                Amount = o.Amount,
                Stage = o.Stage,
                Probability = o.Probability,
                WeightedAmount = o.WeightedAmount,
                ExpectedCloseDate = o.ExpectedCloseDate,
                Status = o.Status,
                AssignedToName = o.AssignedToUser?.FullName ?? "Unassigned",
                CreatedDate = o.CreatedDate
            }).ToList();

            return View(list);
        }

        // GET: Opportunities/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var opp = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedToUser)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opp == null) return NotFound();

            if (!IsAdminOrManager && opp.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            var followUps = await _context.FollowUps
                .Include(f => f.AssignedToUser)
                .Where(f => f.OpportunityId == opp.OpportunityId)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            var activities = await _context.Activities
                .Include(a => a.AssignedToUser)
                .Where(a => a.OpportunityId == opp.OpportunityId)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            ViewBag.FollowUps = followUps;
            ViewBag.Activities = activities;

            return View(opp);
        }

        // GET: Opportunities/Create
        public async Task<IActionResult> Create(int? customerId)
        {
            await PopulateDropdownsAsync();

            var model = new CreateOpportunityViewModel
            {
                CustomerId = customerId ?? 0,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
            };

            return View(model);
        }

        // POST: Opportunities/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateOpportunityViewModel model)
        {
            // Business Validation Rules (Section 5.3, 5.4, 17.7, 17.19)
            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            if (model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (!OpportunityStages.All.Contains(model.Stage))
            {
                ModelState.AddModelError("Stage", "Invalid pipeline stage selected.");
            }

            if (!await _context.Customers.AnyAsync(c => c.CustomerId == model.CustomerId))
            {
                ModelState.AddModelError("CustomerId", "Selected customer does not exist.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(model);
            }

            var assignedUserId = (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedTo))
                ? model.AssignedTo
                : CurrentUserId;

            var status = model.Stage == OpportunityStages.Won ? "Won" 
                       : model.Stage == OpportunityStages.Lost ? "Lost" 
                       : "Open";

            var opp = new Opportunity
            {
                OpportunityName = model.OpportunityName.Trim(),
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                Amount = model.Amount,
                Stage = model.Stage,
                Probability = model.Probability,
                ExpectedCloseDate = model.ExpectedCloseDate,
                Status = status,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = assignedUserId,
                Notes = model.Notes?.Trim()
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "Opportunity",
                recordId: opp.OpportunityId.ToString(),
                newValue: $"Name={opp.OpportunityName}, Amount={opp.Amount}, Stage={opp.Stage}, Prob={opp.Probability}%",
                details: $"Opportunity {opp.OpportunityName} created with Amount {opp.Amount}.");

            TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Opportunities/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();

            if (!IsAdminOrManager && opp.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            await PopulateDropdownsAsync();

            var model = new EditOpportunityViewModel
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                LeadId = opp.LeadId,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status,
                Notes = opp.Notes,
                AssignedTo = opp.AssignedTo
            };

            return View(model);
        }

        // POST: Opportunities/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditOpportunityViewModel model)
        {
            var opp = await _context.Opportunities.FindAsync(model.OpportunityId);
            if (opp == null) return NotFound();

            if (!IsAdminOrManager && opp.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            // Business Validation Rules (Section 5.3, 5.4, 17.7, 17.19)
            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");
            }

            if (model.Probability < 0 || model.Probability > 100)
            {
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
            }

            // Only check close date rule if the opportunity is active
            if (model.Status == "Open" && model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync();
                return View(model);
            }

            var oldSnapshot = $"Stage={opp.Stage}, Amount={opp.Amount}, Prob={opp.Probability}%, Status={opp.Status}";

            opp.OpportunityName = model.OpportunityName.Trim();
            opp.CustomerId = model.CustomerId;
            opp.LeadId = model.LeadId;
            opp.Amount = model.Amount;
            opp.Stage = model.Stage;
            opp.Probability = model.Probability;
            opp.ExpectedCloseDate = model.ExpectedCloseDate;
            opp.Notes = model.Notes?.Trim();

            // Derive status from stage
            if (model.Stage == OpportunityStages.Won)
            {
                opp.Status = "Won";
                opp.Probability = 100;
            }
            else if (model.Stage == OpportunityStages.Lost)
            {
                opp.Status = "Lost";
                opp.Probability = 0;
            }
            else
            {
                opp.Status = "Open";
            }

            if (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedTo))
            {
                opp.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var newSnapshot = $"Stage={opp.Stage}, Amount={opp.Amount}, Prob={opp.Probability}%, Status={opp.Status}";

            await _auditService.LogAsync(
                AuditActions.Update,
                "Opportunity",
                recordId: opp.OpportunityId.ToString(),
                oldValue: oldSnapshot,
                newValue: newSnapshot,
                details: $"Opportunity {opp.OpportunityName} updated.");

            TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Opportunities/QuickStage/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickStage(int id, string targetStage)
        {
            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();

            if (!IsAdminOrManager && opp.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            if (!OpportunityStages.All.Contains(targetStage))
            {
                TempData["ErrorMessage"] = "Invalid target stage.";
                return RedirectToAction(nameof(Index));
            }

            var oldStage = opp.Stage;
            opp.Stage = targetStage;

            if (targetStage == OpportunityStages.Won)
            {
                opp.Status = "Won";
                opp.Probability = 100;
            }
            else if (targetStage == OpportunityStages.Lost)
            {
                opp.Status = "Lost";
                opp.Probability = 0;
            }
            else
            {
                opp.Status = "Open";
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Update,
                "Opportunity",
                recordId: opp.OpportunityId.ToString(),
                oldValue: $"Stage={oldStage}",
                newValue: $"Stage={targetStage}, Status={opp.Status}",
                details: $"Opportunity {opp.OpportunityName} advanced to stage {targetStage}.");

            TempData["SuccessMessage"] = $"Opportunity moved to '{targetStage}'.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Opportunities/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return NotFound();

            if (!IsAdminOrManager && opp.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            var snapshot = $"Name={opp.OpportunityName}, Amount={opp.Amount}";
            _context.Opportunities.Remove(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Delete,
                "Opportunity",
                recordId: id.ToString(),
                oldValue: snapshot,
                details: $"Opportunity {opp.OpportunityName} deleted.");

            TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync()
        {
            var customers = await _context.Customers
                .Where(c => c.Status == CustomerStatuses.Active)
                .OrderBy(c => c.CustomerName)
                .Select(c => new { c.CustomerId, Name = $"{c.CustomerName} ({c.CustomerCode})" })
                .ToListAsync();

            ViewBag.Customers = new SelectList(customers, "CustomerId", "Name");

            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName");
        }
    }
}
