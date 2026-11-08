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
                r.Permissions.Count))
            .ToListAsync(ct);

    public Task<Role?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Roles
            .Include(r => r.Permissions)
            .ThenInclude(p => p.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct = default) =>
        Db.Roles.AnyAsync(r => r.Name == name && (!excludeId.HasValue || r.Id != excludeId.Value), ct);

    public Task<List<Module>> GetMenuModulesAsync(CancellationToken ct = default) =>
        Db.Modules.AsNoTracking().Where(m => m.IsActive).OrderBy(m => m.DisplayOrder).ToListAsync(ct);

    public Task<List<Permission>> GetPermissionsAsync(CancellationToken ct = default) =>
        Db.Permissions.AsNoTracking().OrderBy(p => p.DisplayOrder).ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => Db.Roles.CountAsync(ct);
}
