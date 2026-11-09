using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Dashboard;

namespace SchoolManagementSystem.Application.Services.Dashboard;

/// <summary>Counts follow the working campus; HQ on "All campuses" sees the consolidated numbers.</summary>
public class DashboardService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default)
    {
        var scope = currentUser.DataCampusIds;
        var school = await repositories.Schools.GetDtoAsync(ct);
        var campuses = await repositories.Campuses.CountAsync(scope, ct);
        var activeCampuses = await repositories.Campuses.CountActiveAsync(scope, ct);
        var users = await repositories.Users.CountAsync(scope, ct);
        var activeUsers = await repositories.Users.CountActiveAsync(scope, ct);
        var roles = await repositories.Roles.CountAsync(ct);

        string? activeCampusName = null;
        if (currentUser.ActiveCampusId is int campusId)
        {
            activeCampusName = (await repositories.Campuses.GetDtoAsync(campusId, ct))?.Name;
        }

        var current = await repositories.Sessions.GetEffectiveCurrentAsync(currentUser.ActiveCampusId, ct);
        var sectionCount = current == null ? 0 : await repositories.Sections.CountAsync(current.Id, ct);

        var scopeLabel = activeCampusName ?? (currentUser.IsHq ? "All campuses" : "No campus selected");
        var kpis = new List<KpiCardDto>
        {
            new("campuses", "Campuses", campuses.ToString(), $"{activeCampuses} active", "primary", "/admin/campuses"),
            new("academics", "Current session", current?.Name ?? "Not set", $"{sectionCount} sections", "primary", "/admin/academics/sections"),
            new("users", "Users", users.ToString(), $"{activeUsers} active logins", "neutral", "/admin/users"),
            new("roles", "Roles", roles.ToString(), "Permission templates", "neutral", "/admin/roles"),
            new("hq", "Access mode", currentUser.IsHq ? "HQ" : "Campus", scopeLabel, "primary", null),
        };

        var quickActions = new List<QuickActionDto>();
        if (currentUser.IsHq)
        {
            quickActions.Add(new("add-campus", "Add campus", "Register a new branch with location and in-charge", "/admin/campuses"));
        }

        quickActions.Add(new("add-user", "Add user", "Create a login and assign campus access", "/admin/users"));
        quickActions.Add(new("roles", "Role permissions", "Set View / Add / Edit / Delete per menu", "/admin/roles"));

        return new DashboardDto(
            school?.Name ?? "School Management System",
            school?.ShortName,
            activeCampusName,
            kpis,
            quickActions);
    }
}
