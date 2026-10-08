using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.Models.Entities;

namespace AcxiomCRM.Web.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(
            string action,
            string entityName,
            string? recordId = null,
            string? oldValue = null,
            string? newValue = null,
            string? details = null,
            string result = "Success",
            string? explicitUserId = null,
            string? explicitUserName = null)
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                string? userId = explicitUserId;
                string? userName = explicitUserName;
                string? ipAddress = null;

                if (httpContext != null)
                {
                    if (string.IsNullOrEmpty(userId))
                    {
                        userId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
                    }

                    if (string.IsNullOrEmpty(userName))
                    {
                        userName = httpContext.User?.Identity?.Name;
                    }

                    ipAddress = httpContext.Connection?.RemoteIpAddress?.ToString();
                    if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
                    {
                        ipAddress = "127.0.0.1";
                    }
                }

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    UserName = userName ?? "System",
                    Action = action,
                    EntityName = entityName,
                    RecordId = recordId,
                    OldValue = oldValue,
                    NewValue = newValue,
                    CreatedDate = DateTime.UtcNow,
                    IpAddress = ipAddress ?? "127.0.0.1",
                    Result = result,
                    Details = details
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // In production, log to file or console; never crash user request if audit insert fails
                Console.WriteLine($"[AuditService Error]: {ex.Message}");
            }
        }
    }
}
