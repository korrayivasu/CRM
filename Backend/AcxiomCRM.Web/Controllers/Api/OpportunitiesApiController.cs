using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.Services;

namespace AcxiomCRM.Web.Controllers.Api
{
    [ApiController]
    [Route("api/opportunities")]
    [Authorize]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public OpportunitiesApiController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        // GET: /api/opportunities
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OpportunityDto>>> GetOpportunities([FromQuery] string? search)
        {
            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(o => o.AssignedTo == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(o => o.OpportunityName.ToLower().Contains(term)
                                      || (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term)));
            }

            var list = await query.OrderByDescending(o => o.CreatedDate)
                .Select(o => new OpportunityDto
                {
                    OpportunityId = o.OpportunityId,
                    OpportunityName = o.OpportunityName,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer != null ? o.Customer.CustomerName : null,
                    Amount = o.Amount,
                    Stage = o.Stage,
                    Probability = o.Probability,
                    WeightedAmount = o.WeightedAmount,
                    ExpectedCloseDate = o.ExpectedCloseDate,
                    Status = o.Status,
                    OwnerName = o.AssignedToUser != null ? o.AssignedToUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: /api/opportunities
        [HttpPost]
        public async Task<ActionResult<OpportunityDto>> CreateOpportunity([FromBody] CreateOpportunityDto dto)
        {
            // Business Validation Rules (Section 5.3, 5.4, 17.7, 17.19)
            if (dto.Amount <= 0)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Opportunity Amount must be greater than 0." });
            }

            if (dto.Probability < 0 || dto.Probability > 100)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Probability must be between 0 and 100." });
            }

            if (dto.ExpectedCloseDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Expected Close Date cannot be in the past." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Validation failed.", Errors = ModelState });
            }

            var customer = await _context.Customers.FindAsync(dto.CustomerId);
            if (customer == null)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Selected CustomerId does not exist." });
            }

            var status = dto.Stage == OpportunityStages.Won ? "Won" 
                       : dto.Stage == OpportunityStages.Lost ? "Lost" 
                       : "Open";

            var opp = new Opportunity
            {
                OpportunityName = dto.OpportunityName.Trim(),
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Status = status,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = CurrentUserId,
                Notes = dto.Notes?.Trim()
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "Opportunity API",
                recordId: opp.OpportunityId.ToString(),
                newValue: $"Name={opp.OpportunityName}, Amount={opp.Amount}",
                details: $"Opportunity {opp.OpportunityName} created via REST API.");

            var resultDto = new OpportunityDto
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                CustomerName = customer.CustomerName,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                WeightedAmount = opp.WeightedAmount,
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status
            };

            return StatusCode(201, resultDto);
        }
    }
}
