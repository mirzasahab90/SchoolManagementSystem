using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class ClassRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    private IQueryable<ClassDto> Project(IQueryable<SchoolClass> query) =>
        query.Select(c => new ClassDto(
            c.Id, c.Code, c.Name, c.Stage, c.DisplayOrder, c.IsActive,
            c.CampusId, c.Campus != null ? c.Campus.Code : null,
            c.Subjects.Count,
            Db.Sections.Count(s => s.ClassId == c.Id)));

    /// <summary>Classes visible to the request, in promotion order.</summary>
    public Task<List<ClassDto>> ListAsync(CancellationToken ct = default) =>
        Project(Db.Classes.AsNoTracking().OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)).ToListAsync(ct);

    public async Task<ClassDetailDto?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        var dto = await Project(Db.Classes.AsNoTracking().Where(c => c.Id == id)).FirstOrDefaultAsync(ct);
        if (dto == null) return null;

        var subjects = await Db.ClassSubjects.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(cs => cs.ClassId == id && !cs.Subject.IsDeleted)
            .OrderBy(cs => cs.DisplayOrder)
            .Select(cs => new ClassSubjectDto(cs.SubjectId, cs.Subject.Code, cs.Subject.Name, cs.Subject.Type, cs.IsOptional, cs.DisplayOrder))
            .ToListAsync(ct);
        return new ClassDetailDto(dto, subjects);
    }

    public Task<SchoolClass?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Classes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<SchoolClass?> GetTrackedWithSubjectsAsync(int id, CancellationToken ct = default) =>
        Db.Classes.Include(c => c.Subjects).FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>A visible class, for validating a section's class.</summary>
    public Task<SchoolClass?> GetForSectionAsync(int id, CancellationToken ct = default) =>
        Db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <summary>HQ or the same campus already uses the code (an HQ code must be unused everywhere).</summary>
    public Task<bool> CodeTakenAsync(string code, int? campusId, int? excludeId, CancellationToken ct = default) =>
        Db.Classes.IgnoreQueryFilters().AnyAsync(c =>
            !c.IsDeleted && c.Code == code
            && (!excludeId.HasValue || c.Id != excludeId.Value)
            && (campusId == null || c.CampusId == null || c.CampusId == campusId), ct);

    public Task<int> CountSectionsAsync(int id, CancellationToken ct = default) =>
        Db.Sections.IgnoreQueryFilters().CountAsync(s => !s.IsDeleted && s.ClassId == id, ct);
}
