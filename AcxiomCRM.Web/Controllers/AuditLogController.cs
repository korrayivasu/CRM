using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.Data;
using AcxiomCRM.Web.ViewModels;

namespace AcxiomCRM.Web.Controllers
{
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Manager)]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AuditLog
        public async Task<IActionResult> Index(string? module, string? actionType, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(module))
            {
                query = query.Where(a => a.EntityName == module);
            }

            if (!string.IsNullOrWhiteSpace(actionType))
            {
                query = query.Where(a => a.Action == actionType);
            }

            if (fromDate.HasValue)
            {
                var start = fromDate.Value.Date;
                query = query.Where(a => a.CreatedDate >= start);
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(a => a.CreatedDate <= end);
            }

            var logs = await query.OrderByDescending(a => a.CreatedDate).Take(200).ToListAsync();

            var model = new AuditLogFilterViewModel
            {
                Module = module,
                Action = actionType,
                FromDate = fromDate,
                ToDate = toDate,
                Logs = logs
            };

            ViewBag.Modules = await _context.AuditLogs.Select(a => a.EntityName).Distinct().ToListAsync();
            ViewBag.Actions = await _context.AuditLogs.Select(a => a.Action).Distinct().ToListAsync();

            return View(model);
        }

        // GET: AuditLog/Details/5 (JSON for inspection modal)
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var log = await _context.AuditLogs.FindAsync(id);
            if (log == null) return NotFound();

            return Json(log);
        }
    }
}
