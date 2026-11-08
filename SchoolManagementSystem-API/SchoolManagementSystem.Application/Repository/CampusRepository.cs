using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Org;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class CampusRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public async Task<List<CampusDto>> ListAsync(CancellationToken ct = default)
    {
        return await Db.Campuses.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CampusDto(
                c.Id, c.Code, c.Name, c.Address, c.City, c.State, c.Country, c.PostalCode,
                c.Phone, c.Email, c.Website, c.InChargeName, c.InChargePhone, c.InChargeEmail,
                c.InChargeDesignation, c.EstablishedOn, c.StudentCapacity, c.Notes, c.IsActive,
                c.UserCampuses.Count))
            .ToListAsync(ct);
    }

    public Task<Campus?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Campuses.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct = default) =>
        Db.Campuses.AnyAsync(c => c.Code == code && (!excludeId.HasValue || c.Id != excludeId.Value), ct);

    public Task<School?> GetPrimarySchoolAsync(CancellationToken ct = default) =>
        Db.Schools.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) =>
        Db.Campuses.CountAsync(ct);

    public Task<int> CountActiveAsync(CancellationToken ct = default) =>
        Db.Campuses.CountAsync(c => c.IsActive, ct);
}
