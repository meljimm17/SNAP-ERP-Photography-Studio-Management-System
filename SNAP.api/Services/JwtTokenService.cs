using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SNAP.domain.enums;

namespace SNAP.api.Services
{
    /// <summary>
    /// Issues the JWT a WinForms client holds for every request after logging in. CompanyId (0 for
    /// the SuperAdmin/system-admin login, which has no tenant) is the claim TenantDbContextFactory
    /// reads to resolve which tenant database a request belongs to - the client never gets to say
    /// which database to use, only the server-issued, signed token does.
    /// </summary>
    public class JwtTokenService
    {
        private readonly IConfiguration _configuration;

        public JwtTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(int userId, SystemRole role, int companyId, PlanTier planTier, string displayName, int? branchId = null)
        {
            var key = _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is not configured.");
            var issuer = _configuration["Jwt:Issuer"] ?? "SNAP.api";
            var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 480;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(ClaimTypes.Role, role.ToString()),
                new("companyId", companyId.ToString()),
                new("planTier", planTier.ToString()),
                new(JwtRegisteredClaimNames.Name, displayName)
            };

            // Only branch-bound roles carry this. Its absence is what marks a company-wide role
            // (Admin/FinanceManager/SuperAdmin) as allowed to see every branch - BranchScope reads
            // it rather than trusting any branch id the client sends on the request.
            if (branchId is not null)
            {
                claims.Add(new Claim("branchId", branchId.Value.ToString()));
            }

            var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: issuer,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
