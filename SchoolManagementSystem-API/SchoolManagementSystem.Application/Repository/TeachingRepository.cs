using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Hiring;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

/// <summary>Teaching assignments and the subject lists they are checked against. Campus-filtered by the DbContext.</summary>
public class TeachingRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>Subjects of a class in display order.</summary>
    public Task<List<(int SubjectId, string Name)>> ClassSubjectsAsync(int classId, CancellationToken ct = default) =>
        Db.ClassSubjects.AsNoTracking()
            .Where(cs => cs.ClassId == classId)
            .OrderBy(cs => cs.DisplayOrder).ThenBy(cs => cs.Subject.Name)
            .Select(cs => new ValueTuple<int, string>(cs.SubjectId, cs.Subject.Name))
            .ToListAsync(ct);

    /// <summary>Current (not ended) assignments of these sections with the teacher's name.</summary>
    public async Task<List<(int SectionId, int SubjectId, int StaffId, string TeacherName)>> CurrentHoldersAsync(
        IReadOnlyCollection<int> sectionIds, CancellationToken ct = default)
    {
        var rows = await Db.TeachingAssignments.AsNoTracking()
            .Where(a => a.EndedOn == null && sectionIds.Contains(a.SectionId))
            .Select(a => new { a.SectionId, a.SubjectId, a.StaffId, Name = a.Staff.FirstName + " " + a.Staff.LastName })
            .ToListAsync(ct);
        return rows.Select(r => (r.SectionId, r.SubjectId, r.StaffId, r.Name.Trim())).ToList();
    }

    /// <summary>Active sections of a campus and session with their class, for the Assign dialog.</summary>
    public Task<List<Section>> SectionsAsync(int campusId, int sessionId, CancellationToken ct = default) =>
        Db.Sections.AsNoTracking().Include(s => s.Class)
            .Where(s => s.CampusId == campusId && s.AcademicSessionId == sessionId && s.IsActive)
            .OrderBy(s => s.Class.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);

    /// <summary>Active sections of a campus and session with class and class teacher, for reports.</summary>
    public Task<List<Section>> SectionsWithTeacherAsync(int campusId, int sessionId, CancellationToken ct = default) =>
        Db.Sections.AsNoTracking().Include(s => s.Class).Include(s => s.ClassTeacher)
            .Where(s => s.CampusId == campusId && s.AcademicSessionId == sessionId && s.IsActive)
            .OrderBy(s => s.Class.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);

    public Task<Section?> GetSectionAsync(int id, CancellationToken ct = default) =>
        Db.Sections.Include(s => s.Class).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<TeachingAssignment>> CurrentForStaffAsync(int staffId, CancellationToken ct = default) =>
        Db.TeachingAssignments.Where(a => a.StaffId == staffId && a.EndedOn == null).ToListAsync(ct);

    public Task<List<TeachingAssignment>> OpenForSectionAsync(int sectionId, CancellationToken ct = default) =>
        Db.TeachingAssignments.Where(a => a.SectionId == sectionId && a.EndedOn == null).ToListAsync(ct);

    public void Detach(IEnumerable<TeachingAssignment> items)
    {
        foreach (var item in items) Db.Entry(item).State = EntityState.Detached;
    }

    public Task<List<Section>> ClassTeacherSectionsAsync(int staffId, CancellationToken ct = default) =>
        Db.Sections.Where(s => s.ClassTeacherStaffId == staffId).ToListAsync(ct);

    /// <summary>Every subject of the section's class with its current teacher (or none).</summary>
    public async Task<List<OptionSubjectDto>> SectionTeachersAsync(Section section, CancellationToken ct = default)
    {
        var subjects = await ClassSubjectsAsync(section.ClassId, ct);
        var holders = await CurrentHoldersAsync([section.Id], ct);
        return subjects.Select(sub =>
        {
            var h = holders.FirstOrDefault(x => x.SubjectId == sub.SubjectId);
            return new OptionSubjectDto(sub.SubjectId, sub.Name, h == default ? null : h.StaffId, h == default ? null : h.TeacherName);
        }).ToList();
    }
}
