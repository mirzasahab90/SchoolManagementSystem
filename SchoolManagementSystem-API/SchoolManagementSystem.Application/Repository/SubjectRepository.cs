using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class SubjectRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>Subjects visible to the request (HQ plus the request's campuses), by name.</summary>
    public Task<List<SubjectDto>> ListAsync(CancellationToken ct = default) =>
        Db.Subjects.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(
                s.Id, s.Code, s.Name, s.Type, s.IsActive,
                s.CampusId, s.Campus != null ? s.Campus.Code : null,
                Db.ClassSubjects.Count(cs => cs.SubjectId == s.Id && !cs.Class.IsDeleted)))
            .ToListAsync(ct);

    public Task<Subject?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <summary>HQ or the same campus already uses the code (an HQ code must be unused everywhere).</summary>
    public Task<bool> CodeTakenAsync(string code, int? campusId, int? excludeId, CancellationToken ct = default) =>
        Db.Subjects.IgnoreQueryFilters().AnyAsync(s =>
            !s.IsDeleted && s.Code == code
            && (!excludeId.HasValue || s.Id != excludeId.Value)
            && (campusId == null || s.CampusId == null || s.CampusId == campusId), ct);

    public Task<int> CountClassesUsingAsync(int id, CancellationToken ct = default) =>
        Db.ClassSubjects.IgnoreQueryFilters().CountAsync(cs => cs.SubjectId == id && !cs.Class.IsDeleted, ct);

    /// <summary>Subjects by id that are visible to the request.</summary>
    public Task<List<Subject>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var list = ids.ToList();
        return Db.Subjects.Where(s => list.Contains(s.Id)).ToListAsync(ct);
    }
}
