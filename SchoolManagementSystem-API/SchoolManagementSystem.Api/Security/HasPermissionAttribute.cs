using Microsoft.AspNetCore.Authorization;
using SchoolManagementSystem.Domain.Constants;

namespace SchoolManagementSystem.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute(string module, string action = AppActions.View)
    : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string Module { get; } = module;
    public string Action { get; } = action;

    public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new PermissionRequirement(Module, Action)];
}
