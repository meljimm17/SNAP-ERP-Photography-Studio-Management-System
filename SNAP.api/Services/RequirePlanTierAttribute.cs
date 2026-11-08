using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SNAP.domain.enums;

namespace SNAP.api.Services
{
    /// <summary>
    /// Server-side enforcement of tier-gated modules (Inventory, Equipment, Finance). MainDashboardForm
    /// already hides these from the menu for companies below the required tier, but that's only a UI
    /// convenience - without this, a Micro-tier company could still reach the data by calling the
    /// endpoint directly. Reads the "planTier" claim JwtTokenService embeds in every token at login, so
    /// it reflects what the server decided at login, not anything the client could send.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequirePlanTierAttribute : Attribute, IAuthorizationFilter
    {
        private readonly PlanTier _minimumTier;

        public RequirePlanTierAttribute(PlanTier minimumTier)
        {
            _minimumTier = minimumTier;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                // Not this filter's job - let [Authorize] reject unauthenticated requests with 401.
                return;
            }

            var claim = context.HttpContext.User.FindFirst("planTier")?.Value;
            if (!Enum.TryParse<PlanTier>(claim, out var planTier) || planTier < _minimumTier)
            {
                context.Result = new ObjectResult(new { message = $"This feature requires the {_minimumTier} plan or higher." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
