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
    [Route("api/followups")]
    [Authorize]
    public class FollowUpsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public FollowUpsApiController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        // GET: /api/followups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FollowUpDto>>> GetFollowUps()
        {
            var query = _context.FollowUps
                .Include(f => f.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(f => f.AssignedTo == CurrentUserId);
            }

            var list = await query.OrderByDescending(f => f.FollowUpDate)
                .Select(f => new FollowUpDto
                {
                    FollowUpId = f.FollowUpId,
                    CustomerId = f.CustomerId,
                    LeadId = f.LeadId,
                    OpportunityId = f.OpportunityId,
                    FollowUpDate = f.FollowUpDate,
                    FollowUpType = f.FollowUpType,
                    Subject = f.Subject,
                    Status = f.Status,
                    Remarks = f.Remarks,
                    AssignedToName = f.AssignedToUser != null ? f.AssignedToUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: /api/followups
        [HttpPost]
        public async Task<ActionResult<FollowUpDto>> CreateFollowUp([FromBody] CreateFollowUpDto dto)
        {
            // Business Validation Rule (Section 5.3, 5.4, 17.7, 17.19)
            if (dto.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Follow-up date cannot be earlier than today." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Validation failed.", Errors = ModelState });
            }

            var followUp = new FollowUp
            {
                Subject = dto.Subject.Trim(),
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                Remarks = dto.Remarks?.Trim(),
                Status = FollowUpStatuses.Planned,
                AssignedTo = CurrentUserId,
                CreatedDate = DateTime.UtcNow
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "FollowUp API",
                recordId: followUp.FollowUpId.ToString(),
                newValue: $"Subject={followUp.Subject}, Date={followUp.FollowUpDate:yyyy-MM-dd}",
                details: $"FollowUp {followUp.Subject} created via REST API.");

            var resultDto = new FollowUpDto
            {
                FollowUpId = followUp.FollowUpId,
                CustomerId = followUp.CustomerId,
                LeadId = followUp.LeadId,
                OpportunityId = followUp.OpportunityId,
                FollowUpDate = followUp.FollowUpDate,
                FollowUpType = followUp.FollowUpType,
                Subject = followUp.Subject,
                Status = followUp.Status,
                Remarks = followUp.Remarks
            };

            return StatusCode(201, resultDto);
        }
    }
}
