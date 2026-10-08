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
    [Route("api/leads")]
    [Authorize]
    public class LeadsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public LeadsApiController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        // GET: /api/leads
        [HttpGet]
        public async Task<ActionResult<IEnumerable<LeadDto>>> GetLeads([FromQuery] string? search)
        {
            var query = _context.Leads
                .Include(l => l.AssignedToUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(l => l.AssignedTo == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l => l.LeadName.ToLower().Contains(term) 
                                      || (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)));
            }

            var list = await query.OrderByDescending(l => l.CreatedDate)
                .Select(l => new LeadDto
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
                    AssignedToName = l.AssignedToUser != null ? l.AssignedToUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // POST: /api/leads
        [HttpPost]
        public async Task<ActionResult<LeadDto>> CreateLead([FromBody] CreateLeadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Validation failed.", Errors = ModelState });
            }

            if (!LeadStatuses.All.Contains(dto.Status))
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Invalid lead status." });
            }

            var lastLead = await _context.Leads.OrderByDescending(l => l.LeadId).FirstOrDefaultAsync();
            var nextNum = (lastLead != null ? lastLead.LeadId + 10001 : 10001);

            var lead = new Lead
            {
                LeadCode = $"LEAD-{nextNum}",
                LeadName = dto.LeadName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                Phone = dto.Phone.Trim(),
                CompanyName = dto.CompanyName?.Trim(),
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                CreatedDate = DateTime.UtcNow,
                AssignedTo = CurrentUserId
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "Lead API",
                recordId: lead.LeadId.ToString(),
                newValue: $"Code={lead.LeadCode}, Name={lead.LeadName}",
                details: $"Lead {lead.LeadName} created via REST API.");

            var resultDto = new LeadDto
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
                CreatedDate = lead.CreatedDate
            };

            return StatusCode(201, resultDto);
        }
    }
}
