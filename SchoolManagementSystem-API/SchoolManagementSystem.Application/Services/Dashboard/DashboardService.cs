using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Dashboard;

namespace SchoolManagementSystem.Application.Services.Dashboard;

public class DashboardService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default)
    {
        var school = await repositories.Schools.GetDtoAsync(ct);
        var campuses = await repositories.Campuses.CountAsync(ct);
        var activeCampuses = await repositories.Campuses.CountActiveAsync(ct);
        var users = await repositories.Users.CountAsync(ct);
        var activeUsers = await repositories.Users.CountActiveAsync(ct);
        var roles = await repositories.Roles.CountAsync(ct);

        string? activeCampusName = null;
        if (currentUser.ActiveCampusId is int campusId)
        {
            var list = await repositories.Campuses.ListAsync(ct);
            activeCampusName = list.FirstOrDefault(c => c.Id == campusId)?.Name;
        }

        var kpis = new List<KpiCardDto>
        {
            new("campuses", "Campuses", campuses.ToString(), $"{activeCampuses} active branches", "primary", "/admin/campuses"),
            new("users", "Admin users", users.ToString(), $"{activeUsers} active logins", "neutral", "/admin/users"),
            new("roles", "Roles", roles.ToString(), "Permission templates", "neutral", "/admin/roles"),
            new("hq", "Access mode", currentUser.IsHq ? "HQ" : "Campus", activeCampusName ?? "No campus selected", "primary", null),
        };

        var quickActions = new List<QuickActionDto>
        {
            new("add-campus", "Add campus", "Register a new branch with location and in-charge", "/admin/campuses"),
            new("add-user", "Add admin user", "Create a login and assign campus access", "/admin/users"),
            new("roles", "Role permissions", "Set View / Add / Edit / Delete per menu", "/admin/roles"),
        };

        return new DashboardDto(
            school?.Name ?? "School Management System",
            school?.ShortName,
            activeCampusName,
            kpis,
            quickActions);
    }
}
