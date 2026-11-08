using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SchoolManagementSystem.Domain.Abstractions;

namespace SchoolManagementSystem.Api.Security;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
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

    public int? ActiveCampusId
    {
        get
        {
            var header = httpContextAccessor.HttpContext?.Request.Headers["X-Campus-Id"].FirstOrDefault();
            return int.TryParse(header, out var campusId) ? campusId : null;
        }
    }

    public bool IsHq =>
        httpContextAccessor.HttpContext?.User.FindFirstValue("hq") == "1"
        || httpContextAccessor.HttpContext?.User.IsInRole("SuperAdmin") == true;

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent =>
        httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null;
}
