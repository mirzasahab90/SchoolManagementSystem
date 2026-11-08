using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Auth;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class AccessRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public async Task<UserAccessDto> GetUserAccessAsync(int userId, CancellationToken ct = default)
    {
        var roles = await Db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.Role.IsActive && !ur.Role.IsDeleted)
            .Select(ur => ur.Role.Name)
            .ToListAsync(ct);

        var isSuperAdmin = roles.Contains(SystemRoles.SuperAdmin);
        if (isSuperAdmin)
        {
            return new UserAccessDto(true, roles, new Dictionary<string, HashSet<string>>());
        }

        var rows = await Db.RoleModulePermissions
            .AsNoTracking()
            .Where(rmp => rmp.Role.UserRoles.Any(ur => ur.UserId == userId) && rmp.Role.IsActive && !rmp.Role.IsDeleted)
            .Select(rmp => new { ModuleKey = rmp.Module.Key, Action = rmp.Permission.Code })
            .ToListAsync(ct);

        var map = rows
            .GroupBy(x => x.ModuleKey)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Action).ToHashSet(StringComparer.OrdinalIgnoreCase));

        return new UserAccessDto(false, roles, map);
    }

    public Task<List<Module>> GetActiveModulesAsync(CancellationToken ct = default) =>
        Db.Modules.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.DisplayOrder).ToListAsync(ct);
}
