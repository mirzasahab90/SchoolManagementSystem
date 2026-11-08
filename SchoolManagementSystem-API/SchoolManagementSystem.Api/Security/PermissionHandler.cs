using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;

namespace SchoolManagementSystem.Api.Security;

public class PermissionHandler(IPermissionChecker permissionChecker) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.IsInRole(SystemRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return;
        }

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
