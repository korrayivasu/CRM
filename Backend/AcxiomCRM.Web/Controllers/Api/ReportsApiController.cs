using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.ViewModels;

namespace AcxiomCRM.Web.Controllers.Api
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    public class ReportsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        // GET: /api/reports/pipeline
        [HttpGet("pipeline")]
        public async Task<IActionResult> GetPipelineReport()
        {
            var query = _context.Opportunities.AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(o => o.AssignedTo == CurrentUserId);
            }

            var opps = await query.ToListAsync();
            var totalOpenSum = opps.Where(o => o.Status == "Open").Sum(o => o.Amount);

            var reportRows = OpportunityStages.All.Select(stage =>
            {
                var stageOpps = opps.Where(o => o.Stage == stage).ToList();
                var sum = stageOpps.Sum(o => o.Amount);
                var weighted = stageOpps.Sum(o => o.WeightedAmount);
                var pct = totalOpenSum > 0 ? (double)(sum / totalOpenSum) * 100 : 0;

                return new PipelineReportRow
                {
                    Stage = stage,
                    Count = stageOpps.Count,
                    TotalAmount = sum,
                    WeightedAmount = weighted,
                    PercentageOfPipeline = Math.Round(pct, 1)
                };
            }).ToList();

            return Ok(new
            {
                TotalOpenPipeline = totalOpenSum,
                TotalWeightedForecast = opps.Where(o => o.Status == "Open").Sum(o => o.WeightedAmount),
                Stages = reportRows
            });
        }
    }
}
