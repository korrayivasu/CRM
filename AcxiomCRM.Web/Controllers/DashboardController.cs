using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.ViewModels;

namespace AcxiomCRM.Web.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        public async Task<IActionResult> Index(string? dateFilter = "ThisMonth", DateTime? fromDate = null, DateTime? toDate = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(currentUser!);
            var currentRole = roles.FirstOrDefault() ?? AppRoles.SalesExecutive;

            // Date filtering range
            DateTime? startDate = null;
            DateTime? endDate = null;
            var now = DateTime.UtcNow;

            switch (dateFilter)
            {
                case "Today":
                    startDate = now.Date;
                    endDate = now.Date.AddDays(1).AddTicks(-1);
                    break;
                case "ThisWeek":
                    var diff = (7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7;
                    startDate = now.Date.AddDays(-1 * diff);
                    endDate = startDate.Value.AddDays(7).AddTicks(-1);
                    break;
                case "ThisMonth":
                    startDate = new DateTime(now.Year, now.Month, 1);
                    endDate = startDate.Value.AddMonths(1).AddTicks(-1);
                    break;
                case "Custom":
                    if (fromDate.HasValue) startDate = fromDate.Value.Date;
                    if (toDate.HasValue) endDate = toDate.Value.Date.AddDays(1).AddTicks(-1);
                    break;
                default:
                    // All time / default
                    break;
            }

            // 1. Scoped base queries
            var customersQuery = _context.Customers.AsQueryable();
            var leadsQuery = _context.Leads.AsQueryable();
            var oppsQuery = _context.Opportunities.AsQueryable();
            var followUpsQuery = _context.FollowUps.AsQueryable();
            var activitiesQuery = _context.Activities.AsQueryable();

            if (!IsAdminOrManager)
            {
                customersQuery = customersQuery.Where(c => c.CreatedBy == CurrentUserId);
                leadsQuery = leadsQuery.Where(l => l.AssignedTo == CurrentUserId);
                oppsQuery = oppsQuery.Where(o => o.AssignedTo == CurrentUserId);
                followUpsQuery = followUpsQuery.Where(f => f.AssignedTo == CurrentUserId);
                activitiesQuery = activitiesQuery.Where(a => a.AssignedTo == CurrentUserId);
            }

            // Apply date filters where applicable
            if (startDate.HasValue && endDate.HasValue)
            {
                customersQuery = customersQuery.Where(c => c.CreatedDate >= startDate.Value && c.CreatedDate <= endDate.Value);
                leadsQuery = leadsQuery.Where(l => l.CreatedDate >= startDate.Value && l.CreatedDate <= endDate.Value);
                oppsQuery = oppsQuery.Where(o => o.CreatedDate >= startDate.Value && o.CreatedDate <= endDate.Value);
            }

            // 2. Compute 8 Mandatory KPI Cards (Section 17.11)
            var totalCustomers = await customersQuery.CountAsync();
            var totalLeads = await leadsQuery.CountAsync();
            var openLeads = await leadsQuery.CountAsync(l => l.Status != LeadStatuses.Converted && l.Status != LeadStatuses.Lost);

            var allOppsList = await oppsQuery.ToListAsync();
            var totalOpportunities = allOppsList.Count;
            var openOpportunities = allOppsList.Count(o => o.Status == "Open");
            var wonOpportunities = allOppsList.Count(o => o.Status == "Won" || o.Stage == OpportunityStages.Won);
            var lostOpportunities = allOppsList.Count(o => o.Status == "Lost" || o.Stage == OpportunityStages.Lost);
            var totalPipelineValue = allOppsList.Where(o => o.Status == "Open").Sum(o => o.Amount);
            var weightedPipelineValue = allOppsList.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount);

            // 3. Admin specific metrics
            var totalUsers = await _userManager.Users.CountAsync();
            var activeUsers = await _userManager.Users.CountAsync(u => u.IsActive);
            var recentAuditCount = await _context.AuditLogs.CountAsync();

            // 4. Upcoming Follow-ups (uncompleted, sorted by date)
            var upcomingFollowUps = await followUpsQuery
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Where(f => f.Status == FollowUpStatuses.Planned)
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToListAsync();

            // 5. Recent Activities
            var recentActivities = await activitiesQuery
                .Include(a => a.Customer)
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .ToListAsync();

            // 6. Recent Opportunities
            var recentOpps = allOppsList
                .OrderByDescending(o => o.CreatedDate)
                .Take(5)
                .ToList();

            // 7. Chart 1: Lead Status Chart (Section 17.12)
            // Required statuses: New, Contacted, Qualified, Lost, Converted
            var leadStatuses = new[] { LeadStatuses.New, LeadStatuses.Contacted, LeadStatuses.Qualified, LeadStatuses.Lost, LeadStatuses.Converted };
            var leadCounts = new List<decimal>();
            foreach (var st in leadStatuses)
            {
                leadCounts.Add(await leadsQuery.CountAsync(l => l.Status == st));
            }

            var leadStatusChart = new ChartDataModel
            {
                Labels = leadStatuses.ToList(),
                Data = leadCounts,
                BackgroundColors = new List<string> { "#38bdf8", "#fbbf24", "#34d399", "#94a3b8", "#a855f7" }
            };

            // 8. Chart 2: Opportunity Pipeline Chart (Section 17.12)
            // Required stages: Qualification, Proposal, Negotiation, Won, Lost
            var pipelineStages = new[] { OpportunityStages.Qualification, OpportunityStages.Proposal, OpportunityStages.Negotiation, OpportunityStages.Won, OpportunityStages.Lost };
            var stageAmounts = new List<decimal>();
            foreach (var st in pipelineStages)
            {
                stageAmounts.Add(allOppsList.Where(o => o.Stage == st).Sum(o => o.Amount));
            }

            var opportunityPipelineChart = new ChartDataModel
            {
                Labels = pipelineStages.ToList(),
                Data = stageAmounts,
                BackgroundColors = new List<string> { "#60a5fa", "#f59e0b", "#f97316", "#10b981", "#ef4444" }
            };

            // 9. Chart 3: Monthly Sales / Performance Chart (Section 17.12)
            // Past 6 months won revenue
            var monthlyLabels = new List<string>();
            var monthlyAmounts = new List<decimal>();
            for (int i = 5; i >= 0; i--)
            {
                var monthDate = now.AddMonths(-i);
                var mStart = new DateTime(monthDate.Year, monthDate.Month, 1);
                var mEnd = mStart.AddMonths(1).AddTicks(-1);

                monthlyLabels.Add(mStart.ToString("MMM yyyy"));

                var monthWon = _context.Opportunities
                    .Where(o => (o.Status == "Won" || o.Stage == OpportunityStages.Won)
                             && o.CreatedDate >= mStart && o.CreatedDate <= mEnd);

                if (!IsAdminOrManager)
                {
                    monthWon = monthWon.Where(o => o.AssignedTo == CurrentUserId);
                }

                monthlyAmounts.Add((await monthWon.Select(o => o.Amount).ToListAsync()).Sum());
            }

            var monthlySalesChart = new ChartDataModel
            {
                Labels = monthlyLabels,
                Data = monthlyAmounts,
                BackgroundColors = new List<string> { "#2563eb" }
            };

            var viewModel = new DashboardViewModel
            {
                TotalCustomers = totalCustomers,
                TotalLeads = totalLeads,
                OpenLeads = openLeads,
                TotalOpportunities = totalOpportunities,
                OpenOpportunities = openOpportunities,
                WonOpportunities = wonOpportunities,
                LostOpportunities = lostOpportunities,
                TotalPipelineValue = totalPipelineValue,
                WeightedPipelineValue = weightedPipelineValue,
                DateFilter = dateFilter ?? "ThisMonth",
                CustomFrom = fromDate,
                CustomTo = toDate,
                UserRole = currentRole,
                UserName = currentUser?.FullName ?? User.Identity?.Name ?? "User",
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                RecentAuditEventsCount = recentAuditCount,
                UpcomingFollowUps = upcomingFollowUps,
                RecentActivities = recentActivities,
                RecentOpportunities = recentOpps,
                LeadStatusChart = leadStatusChart,
                OpportunityPipelineChart = opportunityPipelineChart,
                MonthlySalesChart = monthlySalesChart
            };

            return View(viewModel);
        }
    }
}
