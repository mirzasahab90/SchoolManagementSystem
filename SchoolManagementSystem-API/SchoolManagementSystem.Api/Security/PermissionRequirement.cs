using Microsoft.AspNetCore.Authorization;

namespace SchoolManagementSystem.Api.Security;

public class PermissionRequirement(string module, string action) : IAuthorizationRequirement
{
    public string Module { get; } = module;
    public string Action { get; } = action;
}
