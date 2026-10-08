using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
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
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        // GET: Reports / Reports Hub
        public async Task<IActionResult> Index()
        {
            var custQuery = _context.Customers.AsQueryable();
            var leadQuery = _context.Leads.AsQueryable();
            var oppQuery = _context.Opportunities.AsQueryable();
            var followUpQuery = _context.FollowUps.AsQueryable();

            if (!IsAdminOrManager)
            {
                custQuery = custQuery.Where(c => c.CreatedBy == CurrentUserId);
                leadQuery = leadQuery.Where(l => l.AssignedTo == CurrentUserId);
                oppQuery = oppQuery.Where(o => o.AssignedTo == CurrentUserId);
                followUpQuery = followUpQuery.Where(f => f.AssignedTo == CurrentUserId);
            }

            var totalLeads = await leadQuery.CountAsync();
            var convertedLeads = await leadQuery.CountAsync(l => l.Status == LeadStatuses.Converted);
            var leadConvRate = totalLeads > 0 ? (double)convertedLeads / totalLeads * 100 : 0;

            var model = new ReportHubViewModel
            {
                TotalCustomers = await custQuery.CountAsync(),
                TotalLeads = totalLeads,
                TotalOpportunities = await oppQuery.CountAsync(),
                TotalFollowUps = await followUpQuery.CountAsync(),
                PipelineSum = (await oppQuery.Where(o => o.Status == "Open").Select(o => o.Amount).ToListAsync()).Sum(),
                WonSum = (await oppQuery.Where(o => o.Status == "Won" || o.Stage == OpportunityStages.Won).Select(o => o.Amount).ToListAsync()).Sum(),
                LeadConversionRate = Math.Round(leadConvRate, 1)
            };

            return View(model);
        }

        // 1. Customer Report
        public async Task<IActionResult> Customers(string? export)
        {
            var query = _context.Customers
                .Include(c => c.CreatedByUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(c => c.CreatedBy == CurrentUserId);
            }

            var list = await query.OrderByDescending(c => c.CreatedDate).ToListAsync();

            if (export == "csv")
            {
                var sb = new StringBuilder();
                sb.AppendLine("Customer Code,Customer Name,Email,Phone,Company,Status,Owner,Created Date");
                foreach (var c in list)
                {
                    sb.AppendLine($"\"{c.CustomerCode}\",\"{c.CustomerName}\",\"{c.Email}\",\"{c.Phone}\",\"{c.CompanyName}\",\"{c.Status}\",\"{c.CreatedByUser?.FullName ?? "N/A"}\",\"{c.CreatedDate:yyyy-MM-dd}\"");
                }
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Customer_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
            }

            return View(list);
        }

        // 2. Lead Report
        public async Task<IActionResult> Leads(string? export)
        {
            var query = _context.Leads
                .Include(l => l.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(l => l.AssignedTo == CurrentUserId);
            }

            var list = await query.OrderByDescending(l => l.CreatedDate).ToListAsync();

            if (export == "csv")
            {
                var sb = new StringBuilder();
                sb.AppendLine("Lead Code,Lead Name,Email,Phone,Company,Source,Status,Priority,Expected Value,Assigned To,Created Date");
                foreach (var l in list)
                {
                    sb.AppendLine($"\"{l.LeadCode}\",\"{l.LeadName}\",\"{l.Email}\",\"{l.Phone}\",\"{l.CompanyName}\",\"{l.Source}\",\"{l.Status}\",\"{l.Priority}\",\"{l.ExpectedValue}\",\"{l.AssignedToUser?.FullName ?? "N/A"}\",\"{l.CreatedDate:yyyy-MM-dd}\"");
                }
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Lead_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
            }

            return View(list);
        }

        // 3. Follow-Up Report
        public async Task<IActionResult> FollowUps(string? export)
        {
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(f => f.AssignedTo == CurrentUserId);
            }

            var list = await query.OrderByDescending(f => f.FollowUpDate).ToListAsync();

            if (export == "csv")
            {
                var sb = new StringBuilder();
                sb.AppendLine("Subject,Type,FollowUp Date,Status,Assigned To,Remarks");
                foreach (var f in list)
                {
                    sb.AppendLine($"\"{f.Subject}\",\"{f.FollowUpType}\",\"{f.FollowUpDate:yyyy-MM-dd HH:mm}\",\"{f.Status}\",\"{f.AssignedToUser?.FullName ?? "N/A"}\",\"{f.Remarks?.Replace("\"", "\"\"")}\"");
                }
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"FollowUp_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
            }

            return View(list);
        }

        // 4. Opportunity Report
        public async Task<IActionResult> Opportunities(string? export)
        {
            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(o => o.AssignedTo == CurrentUserId);
            }

            var list = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();

            if (export == "csv")
            {
                var sb = new StringBuilder();
                sb.AppendLine("Opportunity Name,Customer,Amount,Stage,Probability,Weighted Amount,Close Date,Status,Owner");
                foreach (var o in list)
                {
                    sb.AppendLine($"\"{o.OpportunityName}\",\"{o.Customer?.CustomerName ?? "N/A"}\",\"{o.Amount}\",\"{o.Stage}\",\"{o.Probability}%\",\"{o.WeightedAmount}\",\"{o.ExpectedCloseDate:yyyy-MM-dd}\",\"{o.Status}\",\"{o.AssignedToUser?.FullName ?? "N/A"}\"");
                }
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Opportunity_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
            }

            return View(list);
        }

        // 5. Pipeline Report (Stage-wise & Weighted)
        public async Task<IActionResult> Pipeline(string? export)
        {
            var query = _context.Opportunities.AsQueryable();
            if (!IsAdminOrManager)
            {
                query = query.Where(o => o.AssignedTo == CurrentUserId);
            }

            var opps = await query.ToListAsync();
            var totalOpen = opps.Where(o => o.Status == "Open").Sum(o => o.Amount);

            var rows = OpportunityStages.All.Select(stage =>
            {
                var inStage = opps.Where(o => o.Stage == stage).ToList();
                var sum = inStage.Sum(o => o.Amount);
                var weighted = inStage.Sum(o => o.WeightedAmount);
                var pct = totalOpen > 0 ? (double)(sum / totalOpen) * 100 : 0;

                return new PipelineReportRow
                {
                    Stage = stage,
                    Count = inStage.Count,
                    TotalAmount = sum,
                    WeightedAmount = weighted,
                    PercentageOfPipeline = Math.Round(pct, 1)
                };
            }).ToList();

            if (export == "csv")
            {
                var sb = new StringBuilder();
                sb.AppendLine("Pipeline Stage,Deals Count,Total Amount,Weighted Amount,% of Open Pipeline");
                foreach (var r in rows)
                {
                    sb.AppendLine($"\"{r.Stage}\",\"{r.Count}\",\"{r.TotalAmount}\",\"{r.WeightedAmount}\",\"{r.PercentageOfPipeline}%\"");
                }
                return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Pipeline_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
            }

            ViewBag.TotalOpen = totalOpen;
            ViewBag.TotalWeighted = opps.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount);
            return View(rows);
        }

        // 6. Conversion Report
        public async Task<IActionResult> Conversion()
        {
            var leadQuery = _context.Leads.AsQueryable();
            var oppQuery = _context.Opportunities.AsQueryable();

            if (!IsAdminOrManager)
            {
                leadQuery = leadQuery.Where(l => l.AssignedTo == CurrentUserId);
                oppQuery = oppQuery.Where(o => o.AssignedTo == CurrentUserId);
            }

            var totalLeads = await leadQuery.CountAsync();
            var convertedLeads = await leadQuery.CountAsync(l => l.Status == LeadStatuses.Converted);

            var opps = await oppQuery.ToListAsync();
            var closedOpps = opps.Where(o => o.Status == "Won" || o.Status == "Lost").ToList();
            var wonOpps = closedOpps.Count(o => o.Status == "Won" || o.Stage == OpportunityStages.Won);
            var lostOpps = closedOpps.Count(o => o.Status == "Lost" || o.Stage == OpportunityStages.Lost);
            var wonRev = closedOpps.Where(o => o.Status == "Won" || o.Stage == OpportunityStages.Won).Sum(o => o.Amount);

            var model = new ConversionReportModel
            {
                TotalLeads = totalLeads,
                ConvertedLeads = convertedLeads,
                LeadConversionRate = totalLeads > 0 ? Math.Round((double)convertedLeads / totalLeads * 100, 1) : 0,
                TotalClosedOpps = closedOpps.Count,
                WonOpps = wonOpps,
                LostOpps = lostOpps,
                OpportunityWinRate = closedOpps.Count > 0 ? Math.Round((double)wonOpps / closedOpps.Count * 100, 1) : 0,
                TotalWonRevenue = wonRev
            };

            return View(model);
        }

        // 7. User Activity Report (Admin & Manager only)
        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager)]
        public async Task<IActionResult> UserActivity()
        {
            var users = await _userManager.Users.ToListAsync();
            var rows = new List<UserActivityReportRow>();

            foreach (var user in users)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                var roleName = userRoles.FirstOrDefault() ?? "Staff";

                var custCount = await _context.Customers.CountAsync(c => c.CreatedBy == user.Id);
                var leadCount = await _context.Leads.CountAsync(l => l.AssignedTo == user.Id);
                var oppCount = await _context.Opportunities.CountAsync(o => o.AssignedTo == user.Id);
                var actCount = await _context.Activities.CountAsync(a => a.AssignedTo == user.Id);
                var fuCount = await _context.FollowUps.CountAsync(f => f.AssignedTo == user.Id);

                rows.Add(new UserActivityReportRow
                {
                    UserName = user.FullName ?? user.UserName ?? "User",
                    Role = roleName,
                    CustomersCreated = custCount,
                    LeadsAssigned = leadCount,
                    OpportunitiesCount = oppCount,
                    ActivitiesLogged = actCount,
                    FollowUpsScheduled = fuCount
                });
            }

            return View(rows);
        }

        // 8. Audit Report (Admin only)
        [Authorize(Roles = AppRoles.Admin)]
        public async Task<IActionResult> Audit()
        {
            var logs = await _context.AuditLogs
                .OrderByDescending(a => a.CreatedDate)
                .Take(250)
                .ToListAsync();

            return View(logs);
        }
    }
}
