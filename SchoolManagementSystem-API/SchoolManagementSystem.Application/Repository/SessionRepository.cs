using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class SessionRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>Sessions visible to the request (HQ plus the request's campuses), newest first.</summary>
    public Task<List<AcademicSessionDto>> ListAsync(CancellationToken ct = default) =>
        Db.AcademicSessions.AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .ThenBy(s => s.CampusId)
            .Select(s => new AcademicSessionDto(
                s.Id, s.Name, s.StartDate, s.EndDate, s.Status, s.IsCurrent,
                s.CampusId, s.Campus != null ? s.Campus.Code : null,
                Db.Sections.Count(x => x.AcademicSessionId == s.Id)))
            .ToListAsync(ct);

    public Task<AcademicSession?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.AcademicSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <summary>The campus's own current session if it has one, otherwise the HQ current session.</summary>
    public async Task<AcademicSession?> GetEffectiveCurrentAsync(int? campusId, CancellationToken ct = default)
    {
        var current = await Db.AcademicSessions.IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && s.IsCurrent && (s.CampusId == null || (campusId != null && s.CampusId == campusId)))
            .ToListAsync(ct);
        return current.FirstOrDefault(s => s.CampusId != null) ?? current.FirstOrDefault(s => s.CampusId == null);
    }

    public Task<List<AcademicSession>> GetCurrentForOwnerAsync(int? campusId, CancellationToken ct = default) =>
        Db.AcademicSessions.IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && s.IsCurrent && s.CampusId == campusId)
            .ToListAsync(ct);

    /// <summary>HQ or the same campus already uses the name (an HQ name must be unused everywhere).</summary>
    public Task<bool> NameTakenAsync(string name, int? campusId, int? excludeId, CancellationToken ct = default) =>
        Db.AcademicSessions.IgnoreQueryFilters().AnyAsync(s =>
            !s.IsDeleted && s.Name == name
            && (!excludeId.HasValue || s.Id != excludeId.Value)
            && (campusId == null || s.CampusId == null || s.CampusId == campusId), ct);

    /// <summary>Dates overlap another session of the same owner.</summary>
    public Task<AcademicSession?> FindOverlapAsync(DateOnly start, DateOnly end, int? campusId, int? excludeId, CancellationToken ct = default) =>
        Db.AcademicSessions.IgnoreQueryFilters().FirstOrDefaultAsync(s =>
            !s.IsDeleted && s.CampusId == campusId
            && (!excludeId.HasValue || s.Id != excludeId.Value)
            && start <= s.EndDate && end >= s.StartDate, ct);

    public Task<bool> HasSectionsAsync(int id, CancellationToken ct = default) =>
        Db.Sections.IgnoreQueryFilters().AnyAsync(s => !s.IsDeleted && s.AcademicSessionId == id, ct);
}
