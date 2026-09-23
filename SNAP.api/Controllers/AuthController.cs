using Microsoft.AspNetCore.Mvc;
using SNAP.api.Services;
using SNAP.domain.enums;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(JwtTokenService jwtTokenService)
        {
            _jwtTokenService = jwtTokenService;
        }

        public sealed record CompanyLoginRequest(string CompanyCode, string Username, string Password);

        public sealed record CompanyLoginResponse(string Token, string Role, string DisplayName, int CompanyId, string PlanTier, int UserId);

        /// <summary>Reuses AuthenticationService.TryAuthenticateCompany as-is for the actual
        /// credential/tier check - what's new here is wrapping the result in a signed JWT instead
        /// of a WinForms LoginForm calling it in-process.</summary>
        [HttpPost("company-login")]
        public ActionResult<CompanyLoginResponse> CompanyLogin(CompanyLoginRequest request)
        {
            var result = AuthenticationService.TryAuthenticateCompany(request.CompanyCode, request.Username, request.Password);
            if (!result.Success)
            {
                return Unauthorized(new { message = result.Message });
            }

            var token = _jwtTokenService.GenerateToken(result.UserId, result.Role, result.CompanyId, result.PlanTier, result.DisplayName);
            return Ok(new CompanyLoginResponse(token, result.Role.ToString(), result.DisplayName, result.CompanyId, result.PlanTier.ToString(), result.UserId));
        }

        public sealed record SystemAdminLoginRequest(string Username, string Password);

        public sealed record SystemAdminLoginResponse(string Token, string Role, string DisplayName);

        [HttpPost("system-admin-login")]
        public ActionResult<SystemAdminLoginResponse> SystemAdminLogin(SystemAdminLoginRequest request)
        {
            var result = AuthenticationService.TryAuthenticateSystemAdmin(request.Username, request.Password);
            if (!result.Success)
            {
                return Unauthorized(new { message = result.Message });
            }

            // CompanyId 0 = no tenant - a SuperAdmin session isn't scoped to any single company's
            // database (see TenantDbContextFactory).
            var token = _jwtTokenService.GenerateToken(userId: 0, result.Role, companyId: 0, PlanTier.Micro, result.DisplayName);
            return Ok(new SystemAdminLoginResponse(token, result.Role.ToString(), result.DisplayName));
        }
    }
}
