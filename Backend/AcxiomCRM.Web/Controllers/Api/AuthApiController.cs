using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Web.Common;
using AcxiomCRM.Web.DTOs;
using AcxiomCRM.Web.Models.Entities;
using AcxiomCRM.Web.Services;

namespace AcxiomCRM.Web.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditService _auditService;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditService auditService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditService = auditService;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ApiErrorResponse { StatusCode = 400, Message = "Invalid request payload.", Errors = ModelState });
            }

            var user = await _userManager.FindByEmailAsync(dto.Email) 
                       ?? await _userManager.FindByNameAsync(dto.Email);

            if (user == null)
            {
                await _auditService.LogAsync(AuditActions.FailedLogin, "Auth API", recordId: dto.Email, details: "API login failed: user not found.", result: "Failed");
                return Unauthorized(new ApiErrorResponse { StatusCode = 401, Message = "Invalid email or password." });
            }

            if (!user.IsActive)
            {
                await _auditService.LogAsync(AuditActions.FailedLogin, "Auth API", recordId: user.Id, details: "API login rejected: deactivated user.", result: "Failed");
                return StatusCode(403, new ApiErrorResponse { StatusCode = 403, Message = "Account is inactive." });
            }

            var result = await _signInManager.PasswordSignInAsync(user.UserName!, dto.Password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                var roles = await _userManager.GetRolesAsync(user);
                await _auditService.LogAsync(AuditActions.Login, "Auth API", recordId: user.Id, details: "API authenticated successfully.", result: "Success");

                return Ok(new LoginResponseDto
                {
                    Succeeded = true,
                    Message = "Authentication successful.",
                    UserId = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "User"
                });
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(AuditActions.Lockout, "Auth API", recordId: user.Id, details: "Account locked out via API.", result: "Failed");
                return StatusCode(423, new ApiErrorResponse { StatusCode = 423, Message = "Account is locked out. Please try again later." });
            }

            await _auditService.LogAsync(AuditActions.FailedLogin, "Auth API", recordId: user.Id, details: "API login failed: bad credentials.", result: "Failed");
            return Unauthorized(new ApiErrorResponse { StatusCode = 401, Message = "Invalid email or password." });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { Message = "Logged out successfully." });
        }
    }
}
