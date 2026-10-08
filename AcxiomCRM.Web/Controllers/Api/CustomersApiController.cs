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
    [Route("api/customers")]
    [Authorize]
    public class CustomersApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public CustomersApiController(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private bool IsAdminOrManager => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Manager);

        // GET: /api/customers
        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers([FromQuery] string? search)
        {
            var query = _context.Customers
                .Include(c => c.CreatedByUser)
                .AsQueryable();

            if (!IsAdminOrManager)
            {
                query = query.Where(c => c.CreatedBy == CurrentUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(c => c.CustomerName.ToLower().Contains(term) 
                                      || c.Email.ToLower().Contains(term)
                                      || c.Phone.Contains(term));
            }

            var list = await query.OrderByDescending(c => c.CreatedDate)
                .Select(c => new CustomerDto
                {
                    CustomerId = c.CustomerId,
                    CustomerCode = c.CustomerCode,
                    CustomerName = c.CustomerName,
                    Email = c.Email,
                    Phone = c.Phone,
                    CompanyName = c.CompanyName,
                    Address = c.Address,
                    City = c.City,
                    State = c.State,
                    Status = c.Status,
                    CreatedDate = c.CreatedDate,
                    OwnerName = c.CreatedByUser != null ? c.CreatedByUser.FullName : null
                })
                .ToListAsync();

            return Ok(list);
        }

        // GET: /api/customers/5
        [HttpGet("{id}")]
        public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
        {
            var c = await _context.Customers
                .Include(x => x.CreatedByUser)
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (c == null)
            {
                return NotFound(new ApiErrorResponse { StatusCode = 404, Message = $"Customer with ID {id} was not found." });
            }

            if (!IsAdminOrManager && c.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            var dto = new CustomerDto
            {
                CustomerId = c.CustomerId,
                CustomerCode = c.CustomerCode,
                CustomerName = c.CustomerName,
                Email = c.Email,
                Phone = c.Phone,
                CompanyName = c.CompanyName,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Status = c.Status,
                CreatedDate = c.CreatedDate,
                OwnerName = c.CreatedByUser?.FullName
            };

            return Ok(dto);
        }

        // POST: /api/customers
        [HttpPost]
        public async Task<ActionResult<CustomerDto>> CreateCustomer([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Validation failed.", Errors = ModelState });
            }

            // Check duplicate email (409 Conflict)
            if (await _context.Customers.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new ApiErrorResponse { StatusCode = 409, Message = "A customer with this email address already exists." });
            }

            // Check duplicate phone (409 Conflict)
            if (await _context.Customers.AnyAsync(c => c.Phone == dto.Phone))
            {
                return Conflict(new ApiErrorResponse { StatusCode = 409, Message = "A customer with this phone number already exists." });
            }

            var lastCust = await _context.Customers.OrderByDescending(c => c.CustomerId).FirstOrDefaultAsync();
            var nextNum = (lastCust != null ? lastCust.CustomerId + 10001 : 10001);
            var customerCode = $"CUST-{nextNum}";

            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = dto.CustomerName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                Phone = dto.Phone.Trim(),
                CompanyName = dto.CompanyName?.Trim(),
                Address = dto.Address?.Trim(),
                City = dto.City?.Trim(),
                State = dto.State?.Trim(),
                Status = dto.Status,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = CurrentUserId
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Create,
                "Customer API",
                recordId: customer.CustomerId.ToString(),
                newValue: $"Code={customer.CustomerCode}, Name={customer.CustomerName}",
                details: $"Customer {customer.CustomerName} created via REST API.");

            var resultDto = new CustomerDto
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                CreatedDate = customer.CreatedDate
            };

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, resultDto);
        }

        // PUT: /api/customers/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Validation failed.", Errors = ModelState });
            }

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new ApiErrorResponse { StatusCode = 404, Message = $"Customer with ID {id} was not found." });
            }

            if (!IsAdminOrManager && customer.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            // Check email uniqueness excluding self
            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Email.ToLower() == dto.Email.ToLower()))
            {
                return Conflict(new ApiErrorResponse { StatusCode = 409, Message = "Email address already in use by another customer." });
            }

            if (await _context.Customers.AnyAsync(c => c.CustomerId != id && c.Phone == dto.Phone))
            {
                return Conflict(new ApiErrorResponse { StatusCode = 409, Message = "Phone number already in use by another customer." });
            }

            customer.CustomerName = dto.CustomerName.Trim();
            customer.Email = dto.Email.Trim().ToLower();
            customer.Phone = dto.Phone.Trim();
            customer.CompanyName = dto.CompanyName?.Trim();
            customer.Address = dto.Address?.Trim();
            customer.City = dto.City?.Trim();
            customer.State = dto.State?.Trim();
            customer.Status = dto.Status;
            customer.ModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Update,
                "Customer API",
                recordId: id.ToString(),
                newValue: $"Name={customer.CustomerName}, Email={customer.Email}",
                details: $"Customer {customer.CustomerCode} updated via REST API.");

            return Ok(new { Message = "Customer updated successfully." });
        }

        // DELETE: /api/customers/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
            {
                return NotFound(new ApiErrorResponse { StatusCode = 404, Message = $"Customer with ID {id} was not found." });
            }

            if (!IsAdminOrManager && customer.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                AuditActions.Delete,
                "Customer API",
                recordId: id.ToString(),
                oldValue: $"Code={customer.CustomerCode}, Name={customer.CustomerName}",
                details: $"Customer {customer.CustomerName} deleted via REST API.");

            return Ok(new { Message = "Customer deleted successfully." });
        }
    }
}
