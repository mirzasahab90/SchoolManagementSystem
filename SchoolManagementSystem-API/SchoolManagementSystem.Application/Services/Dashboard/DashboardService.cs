using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Dashboard;

namespace SchoolManagementSystem.Application.Services.Dashboard;

public class DashboardService(SchoolManagementSystemRepositories repositories) : IDashboardService
{
    public async Task<Result<DashboardDto>> GetAsync(CancellationToken ct = default)
    {
        var campuses = await repositories.Campuses.CountAsync(ct);
        var activeCampuses = await repositories.Campuses.CountActiveAsync(ct);
        var users = await repositories.Users.CountAsync(ct);
        var activeUsers = await repositories.Users.CountActiveAsync(ct);
        var roles = await repositories.Roles.CountAsync(ct);

        var kpis = new List<KpiCardDto>
        {
            new("campuses", "Campuses", campuses.ToString(), $"{activeCampuses} active", "primary", "/admin/campuses"),
            new("users", "Users", users.ToString(), $"{activeUsers} active", "neutral", "/admin/users"),
            new("roles", "Roles", roles.ToString(), "Permission templates", "neutral", "/admin/roles"),
            new("active-campuses", "Active campuses", activeCampuses.ToString(), "Ready for operations", "primary", "/admin/campuses"),
        };

        return new DashboardDto(kpis);
    }
}
