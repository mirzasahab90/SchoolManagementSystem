using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManagementSystem.Application.Interfaces;

namespace SchoolManagementSystem.Api.Security;

public class PermissionHandler(IPermissionChecker permissionChecker) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // SuperAdmin is resolved from the database inside the checker (token role claims can be stale).
        var raw = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var userId))
        {
            return;
        }

        if (await permissionChecker.HasPermissionAsync(userId, requirement.Module, requirement.Action))
        {
            context.Succeed(requirement);
        }
    }
}
