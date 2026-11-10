using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManagementSystem.Application.Interfaces;

namespace SchoolManagementSystem.Api.Security;

/// <summary>
/// Passes when the user holds ANY of the given grants, each written as "module:Action" (e.g. "students:View").
/// Used for read-only lookup lists that several pages need (classes for the admission form, roles for the user form).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class HasAnyPermissionAttribute(params string[] grants) : AuthorizeAttribute, IAuthorizationRequirementData
{
    public IEnumerable<IAuthorizationRequirement> GetRequirements() =>
        [new AnyPermissionRequirement(grants.Select(g =>
        {
            var parts = g.Split(':', 2);
            return (Module: parts[0], Action: parts[1]);
        }).ToList())];
}

public class AnyPermissionRequirement(IReadOnlyList<(string Module, string Action)> grants) : IAuthorizationRequirement
{
    public IReadOnlyList<(string Module, string Action)> Grants { get; } = grants;
}

public class AnyPermissionHandler(IPermissionChecker permissionChecker) : AuthorizationHandler<AnyPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AnyPermissionRequirement requirement)
    {
        var raw = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(raw, out var userId)) return;

        foreach (var (module, action) in requirement.Grants)
        {
            if (await permissionChecker.HasPermissionAsync(userId, module, action))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
