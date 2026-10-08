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
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public LeadsController(
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

        // GET: Leads
        public async Task<IActionResult> Index(string? search, string? statusFilter, string? assignedFilter)
        {
            var query = _context.Leads
                .Include(l => l.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(l => l.AssignedTo == CurrentUserId);
            }

            // Search filter by Lead Name, Company, Status, Assigned User (Section 17.13)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l => l.LeadName.ToLower().Contains(term)
                                      || (l.CompanyName != null && l.CompanyName.ToLower().Contains(term))
                                      || l.Email.ToLower().Contains(term)
                                      || l.Phone.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(l => l.Status == statusFilter);
            }

            if (!string.IsNullOrWhiteSpace(assignedFilter))
            {
                query = query.Where(l => l.AssignedTo == assignedFilter);
            }

            var leads = await query
                .OrderByDescending(l => l.CreatedDate)
                .Select(l => new LeadListViewModel
                {
                    LeadId = l.LeadId,
                    LeadCode = l.LeadCode,
                    LeadName = l.LeadName,
                    Email = l.Email,
                    Phone = l.Phone,
                    CompanyName = l.CompanyName,
                    Source = l.Source,
                    Status = l.Status,
                    Priority = l.Priority,
                    ExpectedValue = l.ExpectedValue,
                    CreatedDate = l.CreatedDate,
                    AssignedToName = l.AssignedToUser != null ? l.AssignedToUser.FullName : "Unassigned",
                    ConvertedCustomerId = l.ConvertedCustomerId
                })
                .ToListAsync();

            ViewData["Search"] = search;
            ViewData["StatusFilter"] = statusFilter;
            ViewData["AssignedFilter"] = assignedFilter;

            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName", assignedFilter);

            return View(leads);
        }

        // GET: Leads/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads
                .Include(l => l.AssignedToUser)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.ConvertedOpportunity)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return NotFound();

            if (!IsAdminOrManager && lead.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            var followUps = await _context.FollowUps
                .Include(f => f.AssignedToUser)
                .Where(f => f.LeadId == lead.LeadId)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            var activities = await _context.Activities
                .Include(a => a.AssignedToUser)
                .Where(a => a.LeadId == lead.LeadId)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            ViewBag.FollowUps = followUps;
            ViewBag.Activities = activities;

            return View(lead);
        }

        // GET: Leads/Create
        public async Task<IActionResult> Create()
        {
            await PopulateUserDropdownAsync();
            return View(new CreateLeadViewModel());
        }

        // POST: Leads/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateLeadViewModel model)
        {
            if (!LeadStatuses.All.Contains(model.Status))
            {
                ModelState.AddModelError("Status", "Invalid lead status selected.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateUserDropdownAsync();
                return View(model);
            }

            var lastLead = await _context.Leads.OrderByDescending(l => l.LeadId).FirstOrDefaultAsync();
            var nextNum = (lastLead != null ? lastLead.LeadId + 10001 : 10001);
            var leadCode = $"LEAD-{nextNum}";

            var assignedUserId = (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedTo))
                ? model.AssignedTo
                : CurrentUserId;

            var lead = new Lead
            {
                LeadCode = leadCode,
                LeadName = model.LeadName.Trim(),
                Email = model.Email.Trim().ToLower(),
                Phone = model.Phone.Trim(),
                CompanyName = model.CompanyName?.Trim(),
                Source = model.Source,
                Status = model.Status,
                Priority = model.Priority,
                ExpectedValue = model.ExpectedValue,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = assignedUserId
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "Lead",
                recordId: lead.LeadId.ToString(),
                newValue: $"Code={lead.LeadCode}, Name={lead.LeadName}, Status={lead.Status}, Value={lead.ExpectedValue}",
                details: $"Lead {lead.LeadName} ({lead.LeadCode}) created.");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' created successfully with code {lead.LeadCode}.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!IsAdminOrManager && lead.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            await PopulateUserDropdownAsync();

            var model = new EditLeadViewModel
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Source = lead.Source,
                Status = lead.Status,
                Priority = lead.Priority,
                ExpectedValue = lead.ExpectedValue,
                AssignedTo = lead.AssignedTo
            };

            return View(model);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditLeadViewModel model)
        {
            var lead = await _context.Leads.FindAsync(model.LeadId);
            if (lead == null) return NotFound();

            if (!IsAdminOrManager && lead.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            // Workflow status transition validation (Section 4.5)
            if (lead.Status == LeadStatuses.Converted && model.Status != LeadStatuses.Converted)
            {
                ModelState.AddModelError("Status", "A converted lead cannot be moved back to active statuses.");
            }

            if (!LeadStatuses.All.Contains(model.Status))
            {
                ModelState.AddModelError("Status", "Invalid lead status selected.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateUserDropdownAsync();
                return View(model);
            }

            var oldSnapshot = $"Name={lead.LeadName}, Status={lead.Status}, Value={lead.ExpectedValue}";

            lead.LeadName = model.LeadName.Trim();
            lead.Email = model.Email.Trim().ToLower();
            lead.Phone = model.Phone.Trim();
            lead.CompanyName = model.CompanyName?.Trim();
            lead.Source = model.Source;
            lead.Status = model.Status;
            lead.Priority = model.Priority;
            lead.ExpectedValue = model.ExpectedValue;

            if (IsAdminOrManager && !string.IsNullOrEmpty(model.AssignedTo))
            {
                lead.AssignedTo = model.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var newSnapshot = $"Name={lead.LeadName}, Status={lead.Status}, Value={lead.ExpectedValue}";

            await _auditService.LogAsync(
                AuditActions.Update,
                "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: oldSnapshot,
                newValue: newSnapshot,
                details: $"Lead {lead.LeadCode} updated.");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Leads/Convert/5
        public async Task<IActionResult> Convert(int? id)
        {
            if (id == null) return NotFound();

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!IsAdminOrManager && lead.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            if (lead.Status == LeadStatuses.Converted)
            {
                TempData["WarningMessage"] = "This lead has already been converted.";
                return RedirectToAction(nameof(Details), new { id = lead.LeadId });
            }

            var model = new ConvertLeadViewModel
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                ExpectedValue = lead.ExpectedValue,
                CreateOpportunity = true,
                OpportunityName = $"{lead.CompanyName ?? lead.LeadName} - Deal",
                OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 100000m,
                Probability = 20,
                ExpectedCloseDate = DateTime.UtcNow.AddDays(30)
            };

            return View(model);
        }

        // POST: Leads/Convert/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(ConvertLeadViewModel model)
        {
            var lead = await _context.Leads.FindAsync(model.LeadId);
            if (lead == null) return NotFound();

            if (!IsAdminOrManager && lead.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            if (lead.Status == LeadStatuses.Converted)
            {
                TempData["ErrorMessage"] = "This lead is already converted.";
                return RedirectToAction(nameof(Index));
            }

            // Business validation on Opportunity if selected (Section 5.3, 17.7)
            if (model.CreateOpportunity)
            {
                if (model.OpportunityAmount <= 0)
                {
                    ModelState.AddModelError("OpportunityAmount", "Opportunity Amount must be greater than 0.");
                }
                if (model.Probability < 0 || model.Probability > 100)
                {
                    ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");
                }
                if (model.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
                {
                    ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 1. Find or create Customer
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email.ToLower() == lead.Email.ToLower() || c.Phone == lead.Phone);

            if (customer == null)
            {
                var lastCust = await _context.Customers.OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
                var nextNum = (lastCust != null ? lastCust.CustomerId + 10001 : 10001);

                customer = new Customer
                {
                    CustomerCode = $"CUST-{nextNum}",
                    CustomerName = lead.LeadName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyName = lead.CompanyName,
                    Status = CustomerStatuses.Active,
                    CreatedDate = DateTime.UtcNow,
                    CreatedBy = lead.AssignedTo
                };
                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
            }

            // 2. Create Opportunity if selected
            Opportunity? opp = null;
            if (model.CreateOpportunity)
            {
                opp = new Opportunity
                {
                    OpportunityName = string.IsNullOrWhiteSpace(model.OpportunityName) ? $"{lead.LeadName} - Deal" : model.OpportunityName.Trim(),
                    CustomerId = customer.CustomerId,
                    LeadId = lead.LeadId,
                    Amount = model.OpportunityAmount,
                    Stage = OpportunityStages.Qualification,
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate,
                    Status = "Open",
                    CreatedDate = DateTime.UtcNow,
                    AssignedTo = lead.AssignedTo,
                    Notes = $"Converted from Lead {lead.LeadCode} ({lead.LeadName})."
                };
                _context.Opportunities.Add(opp);
                await _context.SaveChangesAsync();
            }

            // 3. Mark Lead as Converted
            lead.Status = LeadStatuses.Converted;
            lead.ConvertedCustomerId = customer.CustomerId;
            if (opp != null)
            {
                lead.ConvertedOpportunityId = opp.OpportunityId;
            }
            await _context.SaveChangesAsync();

            // 4. Log conversion event in Audit Log per Section 8 & Section 17.16
            await _auditService.LogAsync(
                AuditActions.LeadConverted,
                "Lead",
                recordId: lead.LeadId.ToString(),
                oldValue: $"Status=Qualified",
                newValue: $"Status=Converted, CustomerId={customer.CustomerId}, OppId={(opp?.OpportunityId.ToString() ?? "None")}",
                details: $"Lead {lead.LeadCode} converted to Customer {customer.CustomerCode}." + (opp != null ? $" Created Opportunity {opp.OpportunityName}." : ""));

            TempData["SuccessMessage"] = $"Lead successfully converted! Customer '{customer.CustomerName}' created/linked." + (opp != null ? " Opportunity created." : "");
            return RedirectToAction(nameof(Details), new { id = lead.LeadId });
        }

        // POST: Leads/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return NotFound();

            if (!IsAdminOrManager && lead.AssignedTo != CurrentUserId)
            {
                return Forbid();
            }

            var snapshot = $"Code={lead.LeadCode}, Name={lead.LeadName}";
            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Delete,
                "Lead",
                recordId: id.ToString(),
                oldValue: snapshot,
                details: $"Lead {lead.LeadCode} deleted.");

            TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' was successfully deleted.";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateUserDropdownAsync()
        {
            var users = await _userManager.Users.Where(u => u.IsActive).OrderBy(u => u.FullName).ToListAsync();
            ViewBag.Users = new SelectList(users, "Id", "FullName");
        }
    }
}
