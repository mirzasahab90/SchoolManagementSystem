using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

/// <summary>Sections are campus data: every query here is narrowed to the request's campuses by the EF filter.</summary>
public class SectionRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<List<SectionDto>> ListAsync(int sessionId, int? classId, CancellationToken ct = default) =>
        Db.Sections.AsNoTracking()
            .Where(s => s.AcademicSessionId == sessionId && (classId == null || s.ClassId == classId))
            .OrderBy(s => s.Campus.Name).ThenBy(s => s.Class.DisplayOrder).ThenBy(s => s.Name)
            .Select(s => new SectionDto(
                s.Id, s.CampusId, s.Campus.Code, s.Campus.Name, s.AcademicSessionId, s.AcademicSession.Name,
                s.ClassId, s.Class.Code, s.Class.Name, s.Class.DisplayOrder, s.Name, s.Shift, s.Capacity, s.Room, s.IsActive))
            .ToListAsync(ct);

    public async Task<SectionDto?> GetDtoAsync(int id, CancellationToken ct = default)
    {
        var section = await Db.Sections.AsNoTracking().Where(s => s.Id == id).Select(s => new { s.AcademicSessionId }).FirstOrDefaultAsync(ct);
        return section == null ? null : (await ListAsync(section.AcademicSessionId, null, ct)).FirstOrDefault(s => s.Id == id);
    }

    public Task<Section?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Sections.Include(s => s.AcademicSession).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> ExistsAsync(int campusId, int sessionId, int classId, string name, int? excludeId, CancellationToken ct = default) =>
        Db.Sections.IgnoreQueryFilters().AnyAsync(s =>
            !s.IsDeleted && s.CampusId == campusId && s.AcademicSessionId == sessionId && s.ClassId == classId
            && s.Name == name && (!excludeId.HasValue || s.Id != excludeId.Value), ct);

    /// <summary>Active sections of one campus and session whose class is still active.</summary>
    public Task<List<Section>> ListForCopyAsync(int campusId, int sessionId, CancellationToken ct = default) =>
        Db.Sections.AsNoTracking()
            .Where(s => s.CampusId == campusId && s.AcademicSessionId == sessionId && s.IsActive && s.Class.IsActive)
            .ToListAsync(ct);

    public Task<int> CountAsync(int sessionId, CancellationToken ct = default) =>
        Db.Sections.CountAsync(s => s.AcademicSessionId == sessionId, ct);
}
