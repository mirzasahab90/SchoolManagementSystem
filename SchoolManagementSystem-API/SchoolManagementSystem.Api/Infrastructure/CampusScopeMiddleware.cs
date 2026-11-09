using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.DTOs;

namespace SchoolManagementSystem.Api.Infrastructure;

/// <summary>
/// Runs after authentication. Loads the user's HQ status and campuses from the database and validates
/// the requested working campus (X-Campus-Id). Inactive users are rejected even with a valid token.
/// </summary>
public class CampusScopeMiddleware(RequestDelegate next)
{
    public const string CampusHeader = "X-Campus-Id";
    public const string InvalidCampusHeader = "X-Campus-Invalid";

    public async Task InvokeAsync(HttpContext context, CampusScope scope, SchoolManagementSystemRepositories repositories)
    {
        var raw = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || !int.TryParse(raw, out var userId))
        {
            await next(context);
            return;
        }

        var ct = context.RequestAborted;
        var access = await repositories.Access.GetCampusAccessAsync(userId, ct);
        if (access == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(
                new ResponseDto<object>(null, 401, "Your account is inactive. Please contact your administrator."), ct);
            return;
        }

        // A temporary password only opens the auth endpoints (me, change-password, logout).
        if (access.MustChangePassword && !context.Request.Path.StartsWithSegments("/api/auth"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new ResponseDto<object>(null, 403, "Change your password before continuing."), ct);
            return;
        }

        var assigned = access.CampusIds.ToHashSet();
        int? active = access.IsHq ? null : access.CampusIds.Count > 0 ? access.CampusIds[0] : null;

        var header = context.Request.Headers[CampusHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header))
        {
            var valid = int.TryParse(header, out var requested)
                        && (access.IsHq
                            ? await repositories.Campuses.IsActiveAsync(requested, ct)
                            : assigned.Contains(requested));

            if (valid)
            {
                active = requested;
            }
            else if (!context.Request.Path.StartsWithSegments("/api/auth"))
            {
                // Auth endpoints fall back to the default campus so the client can recover its state.
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers[InvalidCampusHeader] = "1";
                await context.Response.WriteAsJsonAsync(
                    new ResponseDto<object>(null, 403, "You do not have access to the selected campus."), ct);
                return;
            }
        }

        scope.Resolve(access.IsHq, access.IsSuperAdmin, assigned, active);
        await next(context);
    }
}
