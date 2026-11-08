using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Org;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class SchoolRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<School?> GetTrackedAsync(CancellationToken ct = default) =>
        Db.Schools.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

    public async Task<SchoolDto?> GetDtoAsync(CancellationToken ct = default)
    {
        var school = await Db.Schools.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        return school == null
            ? null
            : new SchoolDto(school.Id, school.Name, school.ShortName, school.Phone, school.Email, school.Address);
    }
}
