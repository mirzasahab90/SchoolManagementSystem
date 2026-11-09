using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Org;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class CampusRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>All campuses, or only <paramref name="campusIds"/> when given (non-HQ users).</summary>
    public async Task<List<CampusDto>> ListAsync(IReadOnlySet<int>? campusIds = null, CancellationToken ct = default)
    {
        return await Db.Campuses.AsNoTracking()
            .Where(c => campusIds == null || campusIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new CampusDto(
                c.Id, c.Code, c.Name, c.Address, c.City, c.State, c.Country, c.PostalCode,
                c.Phone, c.Email, c.Website, c.InChargeName, c.InChargePhone, c.InChargeEmail,
                c.InChargeDesignation, c.EstablishedOn, c.StudentCapacity, c.Notes, c.IsActive,
                c.UserCampuses.Count,
                Db.Users
                    .Where(u => u.IsActive
                                && u.UserCampuses.Any(uc => uc.CampusId == c.Id)
                                && u.UserRoles.Any(ur => ur.Role.Name == SystemRoles.CampusAdmin && ur.Role.CampusId == null))
                    .OrderBy(u => u.FirstName)
                    .Select(u => new CampusAdminRefDto(u.Id, u.FirstName + " " + u.LastName, u.Email))
                    .ToList()))
            .ToListAsync(ct);
    }

    public async Task<CampusDto?> GetDtoAsync(int id, CancellationToken ct = default) =>
        (await ListAsync(new HashSet<int> { id }, ct)).FirstOrDefault();

    public Task<bool> IsActiveAsync(int id, CancellationToken ct = default) =>
        Db.Campuses.AsNoTracking().AnyAsync(c => c.Id == id && c.IsActive, ct);

    /// <summary>Non-HQ users for whom this is the only campus (they would lose all access).</summary>
    public Task<int> CountUsersOnlyInCampusAsync(int id, CancellationToken ct = default) =>
        Db.Users.AsNoTracking().CountAsync(u => !u.IsHq
                                                && u.UserCampuses.Any(uc => uc.CampusId == id)
                                                && u.UserCampuses.All(uc => uc.CampusId == id), ct);

    public Task<Campus?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Campuses.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct = default) =>
        Db.Campuses.AnyAsync(c => c.Code == code && (!excludeId.HasValue || c.Id != excludeId.Value), ct);

    public Task<School?> GetPrimarySchoolAsync(CancellationToken ct = default) =>
        Db.Schools.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

    public Task<int> CountAsync(IReadOnlyCollection<int>? campusIds = null, CancellationToken ct = default) =>
        Db.Campuses.CountAsync(c => campusIds == null || campusIds.Contains(c.Id), ct);

    public Task<int> CountActiveAsync(IReadOnlyCollection<int>? campusIds = null, CancellationToken ct = default) =>
        Db.Campuses.CountAsync(c => c.IsActive && (campusIds == null || campusIds.Contains(c.Id)), ct);
}
