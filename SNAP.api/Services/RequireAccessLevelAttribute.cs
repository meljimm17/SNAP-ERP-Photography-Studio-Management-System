using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SNAP.domain.enums;
using SNAP.infrastructure.services;

namespace SNAP.api.Services
{
    /// <summary>
    /// Server-side enforcement of PermissionService's role-to-module access levels - the same
    /// pattern RequirePlanTierAttribute already uses for tier gating, just checking AccessLevel
    /// instead of PlanTier. WinForms already hides/disables buttons below a role's access level for
    /// a given module (MainDashboardForm, the various *Form.cs screens), but that's a UI
    /// convenience only - without this, any authenticated user of any role could still create,
    /// edit, delete or even just read through an endpoint by calling it directly, regardless of
    /// what PermissionService says their role may actually do. Confirmed exploitable before this
    /// was added: a Photographer (CustomerManagement = AssignedView, i.e. read-only) could create a
    /// brand new Customer via a direct API call, since nothing server-side checked their role at
    /// all beyond "are they logged in somewhere".
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireAccessLevelAttribute : Attribute, IAuthorizationFilter
    {
        private static readonly PermissionService Permissions = new();

        private readonly SystemModule _module;
        private readonly AccessLevel _minimumLevel;

        public RequireAccessLevelAttribute(SystemModule module, AccessLevel minimumLevel)
        {
            _module = module;
            _minimumLevel = minimumLevel;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                // Not this filter's job - let [Authorize] reject unauthenticated requests with 401.
                return;
            }

            var roleClaim = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (!Enum.TryParse<SystemRole>(roleClaim, out var role)
                || Permissions.GetAccessLevel(role, _module) < _minimumLevel)
            {
                context.Result = new ObjectResult(new { message = "You don't have permission to perform this action." })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
    }
}
