using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SchoolManagementSystem.Domain.Abstractions;

namespace SchoolManagementSystem.Api.Security;

/// <summary>
/// Identity comes from the JWT; campus access and HQ status come from <see cref="CampusScope"/>,
/// which the campus-scope middleware loads from the database (never trusted from the client).
/// </summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor, CampusScope scope) : ICurrentUser
{
    public int? UserId
    {
        get
        {
            var raw = httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : null;
        }
    }

    public int? ActiveCampusId => scope.ActiveCampusId;

    public bool IsHq => scope.IsHq;

    public bool IsSuperAdmin => scope.IsSuperAdmin;

    public IReadOnlySet<int> AssignedCampusIds => scope.AssignedCampusIds;

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent =>
        httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;
}
