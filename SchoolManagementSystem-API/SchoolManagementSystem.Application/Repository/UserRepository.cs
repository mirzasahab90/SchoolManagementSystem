using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Users;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class UserRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        Db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetWithRolesAndCampusesAsync(int id, CancellationToken ct = default) =>
        Db.Users
            .Include(u => u.UserRoles)
            .Include(u => u.UserCampuses)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetActiveReadOnlyAsync(int id, CancellationToken ct = default) =>
        Db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.IsActive, ct);

    public Task<bool> EmailExistsAsync(string email, int? excludeId = null, CancellationToken ct = default) =>
        Db.Users.AnyAsync(u => u.Email == email && (!excludeId.HasValue || u.Id != excludeId.Value), ct);

    /// <summary>
    /// All users, or — when <paramref name="campusIds"/> is given (campus admins) — non-HQ users
    /// who belong to at least one of those campuses.
    /// </summary>
    public async Task<List<UserListItemDto>> ListAsync(IReadOnlySet<int>? campusIds = null, int? userId = null, CancellationToken ct = default)
    {
        var users = await Db.Users.AsNoTracking()
            .Where(u => campusIds == null || (!u.IsHq && u.UserCampuses.Any(uc => campusIds.Contains(uc.CampusId))))
            .Where(u => userId == null || u.Id == userId)
            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            .Select(u => new
            {
                u.Id,
                u.FirstName,
                u.LastName,
                u.Email,
                u.Phone,
                u.IsActive,
                u.IsHq,
                u.LastLoginOn,
                RoleIds = u.UserRoles.Select(ur => ur.RoleId).ToList(),
                CampusIds = u.UserCampuses.Select(uc => uc.CampusId).ToList()
            })
            .ToListAsync(ct);

        // Role names are read without the ownership filter: hiding a role here would make an editor
        // drop it on save. Names of other campuses' roles are not sensitive.
        var roleIds = users.SelectMany(u => u.RoleIds).Distinct().ToList();
        var roleNames = await Db.Roles.IgnoreQueryFilters().AsNoTracking()
            .Where(r => roleIds.Contains(r.Id) && !r.IsDeleted)
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        return users.Select(u => new UserListItemDto(
            u.Id, u.FirstName, u.LastName, $"{u.FirstName} {u.LastName}".Trim(),
            u.Email, u.Phone, u.IsActive, u.IsHq, u.LastLoginOn,
            u.RoleIds.Where(roleNames.ContainsKey).Select(id => new RoleRefDto(id, roleNames[id])).ToList(),
            u.CampusIds)).ToList();
    }

    public async Task<UserListItemDto?> GetListItemAsync(int id, IReadOnlySet<int>? campusIds = null, CancellationToken ct = default) =>
        (await ListAsync(campusIds, id, ct)).FirstOrDefault();

    public async Task<List<(int CampusId, string Code, string Name, bool IsPrimary)>> GetUserCampusesAsync(int userId, CancellationToken ct = default)
    {
        return await Db.UserCampuses
            .AsNoTracking()
            .Where(uc => uc.UserId == userId && uc.Campus.IsActive && !uc.Campus.IsDeleted)
            .OrderByDescending(uc => uc.IsPrimary)
            .ThenBy(uc => uc.Campus.Name)
            .Select(uc => new ValueTuple<int, string, string, bool>(uc.CampusId, uc.Campus.Code, uc.Campus.Name, uc.IsPrimary))
            .ToListAsync(ct);
    }

    public Task<bool> UserHasCampusAsync(int userId, int campusId, CancellationToken ct = default) =>
        Db.UserCampuses.AsNoTracking().AnyAsync(uc => uc.UserId == userId && uc.CampusId == campusId, ct);

    public Task<int> CountAsync(IReadOnlyCollection<int>? campusIds = null, CancellationToken ct = default) =>
        Db.Users.CountAsync(u => campusIds == null || u.UserCampuses.Any(uc => campusIds.Contains(uc.CampusId)), ct);

    public Task<int> CountActiveAsync(IReadOnlyCollection<int>? campusIds = null, CancellationToken ct = default) =>
        Db.Users.CountAsync(u => u.IsActive && (campusIds == null || u.UserCampuses.Any(uc => campusIds.Contains(uc.CampusId))), ct);

    /// <summary>
    /// Roles by id regardless of the request's campus view (a user may hold another campus's role the editor
    /// cannot see). Callers enforce who may assign which role.
    /// </summary>
    public Task<List<Role>> GetRolesByIdsAsync(IEnumerable<int> roleIds, CancellationToken ct = default) =>
        Db.Roles.IgnoreQueryFilters().Where(r => roleIds.Contains(r.Id) && r.IsActive && !r.IsDeleted).ToListAsync(ct);

    public void ReplaceRoles(User user, IEnumerable<int> roleIds, int? assignedBy)
    {
        user.UserRoles.Clear();
        foreach (var roleId in roleIds.Distinct())
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = roleId,
                AssignedOn = DateTime.UtcNow,
                AssignedBy = assignedBy
            });
        }
    }

    public void ReplaceCampuses(User user, IReadOnlyList<int> campusIds)
    {
        user.UserCampuses.Clear();
        var primary = true;
        foreach (var campusId in campusIds.Distinct())
        {
            user.UserCampuses.Add(new UserCampus
            {
                UserId = user.Id,
                CampusId = campusId,
                IsPrimary = primary
            });
            primary = false;
        }
    }
}
