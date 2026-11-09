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
        // Ignore query filters: permissions are resolved outside the request's campus scope
        // (the checker runs in its own DI scope), and campus-owned roles must still count.
        var roles = await Db.UserRoles
            .AsNoTracking()
            .IgnoreQueryFilters()
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
            .IgnoreQueryFilters()
            .Where(rmp => rmp.Role.UserRoles.Any(ur => ur.UserId == userId) && rmp.Role.IsActive && !rmp.Role.IsDeleted && rmp.Module.IsActive)
            .Select(rmp => new { ModuleKey = rmp.Module.Key, Action = rmp.Permission.Code })
            .ToListAsync(ct);

        var map = rows
            .GroupBy(x => x.ModuleKey)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Action).ToHashSet(StringComparer.OrdinalIgnoreCase));

        return new UserAccessDto(false, roles, map);
    }

    /// <summary>Null when the user does not exist or is inactive. CampusIds are active campuses, primary first.</summary>
    public async Task<UserCampusAccessDto?> GetCampusAccessAsync(int userId, CancellationToken ct = default)
    {
        var user = await Db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => new { u.IsHq, u.MustChangePassword })
            .FirstOrDefaultAsync(ct);
        if (user == null)
        {
            return null;
        }

        var isSuperAdmin = await Db.UserRoles.AsNoTracking()
            .AnyAsync(ur => ur.UserId == userId
                            && ur.Role.Name == SystemRoles.SuperAdmin
                            && ur.Role.IsActive
                            && !ur.Role.IsDeleted, ct);

        var campusIds = await Db.UserCampuses.AsNoTracking()
            .Where(uc => uc.UserId == userId && uc.Campus.IsActive && !uc.Campus.IsDeleted)
            .OrderByDescending(uc => uc.IsPrimary)
            .ThenBy(uc => uc.Campus.Name)
            .Select(uc => uc.CampusId)
            .ToListAsync(ct);

        return new UserCampusAccessDto(user.IsHq || isSuperAdmin, isSuperAdmin, campusIds, user.MustChangePassword);
    }

    public Task<List<Module>> GetActiveModulesAsync(CancellationToken ct = default) =>
        Db.Modules.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.DisplayOrder).ToListAsync(ct);
}
