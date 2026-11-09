using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Org;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class RoleRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<List<RoleListItemDto>> ListAsync(CancellationToken ct = default) =>
        Db.Roles.AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => new RoleListItemDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystem,
                r.IsActive,
                r.UserRoles.Count,
                r.Permissions.Count,
                r.CampusId,
                r.Campus != null ? r.Campus.Code : null,
                r.HqAssignOnly))
            .ToListAsync(ct);

    public Task<Role?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Roles
            .Include(r => r.Permissions)
            .ThenInclude(p => p.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <summary>
    /// A name is taken when HQ or the same campus already uses it. An HQ role's name must also be
    /// unused by every campus, so one name never means two different roles.
    /// </summary>
    public Task<bool> NameTakenAsync(string name, int? campusId, int? excludeId, CancellationToken ct = default) =>
        Db.Roles.IgnoreQueryFilters().AnyAsync(r =>
            !r.IsDeleted
            && r.Name == name
            && (!excludeId.HasValue || r.Id != excludeId.Value)
            && (campusId == null || r.CampusId == null || r.CampusId == campusId), ct);

    /// <summary>Leaf menus only (have a route) — group parents like Settings are not permission targets.</summary>
    public Task<List<Module>> GetMenuModulesAsync(CancellationToken ct = default) =>
        Db.Modules.AsNoTracking()
            .Where(m => m.IsActive && m.Route != null && m.Route != "")
            .OrderBy(m => m.ParentId)
            .ThenBy(m => m.DisplayOrder)
            .ToListAsync(ct);

    public Task<List<Permission>> GetPermissionsAsync(CancellationToken ct = default) =>
        Db.Permissions.AsNoTracking().OrderBy(p => p.DisplayOrder).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => Db.Roles.CountAsync(ct);

    public Task<int> CountUsersAsync(int roleId, CancellationToken ct = default) =>
        Db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);

    public Task<bool> UserHasRoleAsync(int userId, int roleId, CancellationToken ct = default) =>
        Db.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);

    /// <summary>(module key, action) pairs granted by the given roles, ignoring campus view.</summary>
    public async Task<List<(string Module, string Action)>> GetGrantsAsync(IEnumerable<int> roleIds, CancellationToken ct = default)
    {
        var ids = roleIds.ToList();
        var rows = await Db.RoleModulePermissions.AsNoTracking().IgnoreQueryFilters()
            .Where(x => ids.Contains(x.RoleId) && !x.Role.IsDeleted && x.Role.IsActive)
            .Select(x => new { x.Module.Key, x.Permission.Code })
            .ToListAsync(ct);
        return rows.Select(x => (x.Key, x.Code)).ToList();
    }

    public Task<int?> GetHqRoleIdAsync(string name, CancellationToken ct = default) =>
        Db.Roles.IgnoreQueryFilters()
            .Where(r => !r.IsDeleted && r.CampusId == null && r.Name == name)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(ct);

    public void SoftDelete(Role role) => Db.Roles.Remove(role);
}
