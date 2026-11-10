using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>Teaching assignments (section, subject) in the current session, and class teachers.</summary>
public partial class StaffService
{
    private static readonly Error NoCurrentSession = Error.Validation("Set a current session first.");

    public async Task<Result<AssignmentOptionsDto>> GetAssignmentOptionsAsync(int id, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(staff.CampusId, ct);
        if (session == null) return NoCurrentSession;

        var sections = await repositories.Teaching.SectionsAsync(staff.CampusId, session.Id, ct);
        var holders = await repositories.Teaching.CurrentHoldersAsync(sections.Select(s => s.Id).ToList(), ct);
        var subjectCache = new Dictionary<int, List<(int SubjectId, string Name)>>();
        var options = new List<OptionSectionDto>();
        foreach (var section in sections)
        {
            if (!subjectCache.TryGetValue(section.ClassId, out var subjects))
            {
                subjects = await repositories.Teaching.ClassSubjectsAsync(section.ClassId, ct);
                subjectCache[section.ClassId] = subjects;
            }

            options.Add(new OptionSectionDto(section.Id, section.Class.Name, section.Name, subjects.Select(sub =>
            {
                var h = holders.FirstOrDefault(x => x.SectionId == section.Id && x.SubjectId == sub.SubjectId);
                return new OptionSubjectDto(sub.SubjectId, sub.Name, h == default ? null : h.StaffId, h == default ? null : h.TeacherName);
            }).ToList()));
        }

        return new AssignmentOptionsDto(session.Id, session.Name, options);
    }

    public async Task<Result<StaffProfileDto>> AssignAsync(int id, AssignRequest request, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        var error = CanTeach(staff);
        if (error != null) return error;
        if (request.SubjectIds == null || request.SubjectIds.Count == 0) return Error.Validation("Pick at least one subject.");

        var section = await repositories.Teaching.GetSectionAsync(request.SectionId, ct);
        if (section == null || section.CampusId != staff.CampusId) return Error.Validation("Pick a section of this campus.");
        if (!section.IsActive) return Error.Validation("That section is inactive.");
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(staff.CampusId, ct);
        if (session == null) return NoCurrentSession;
        if (section.AcademicSessionId != session.Id) return Error.Validation("Pick a section of the current session.");

        var subjects = await repositories.Teaching.ClassSubjectsAsync(section.ClassId, ct);
        var holders = await repositories.Teaching.CurrentHoldersAsync([section.Id], ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var subjectId in request.SubjectIds.Distinct())
        {
            var subject = subjects.FirstOrDefault(s => s.SubjectId == subjectId);
            if (subject == default) return Error.Validation($"That subject is not taught in {section.Class.Name}.");
            var holder = holders.FirstOrDefault(h => h.SubjectId == subjectId);
            if (holder != default && holder.StaffId == staff.Id) continue; // already theirs
            if (holder != default)
                return Error.Conflict($"{section.Class.Name} {section.Name} {subject.Name} is already taught by {holder.TeacherName}.");

            staff.Assignments.Add(new TeachingAssignment
            {
                CampusId = staff.CampusId,
                AcademicSessionId = section.AcademicSessionId,
                SectionId = section.Id,
                SubjectId = subjectId,
                StartedOn = today
            });
        }

        await repositories.SaveChangesAsync(ct); // a racing duplicate hits the unique index → 409
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<StaffProfileDto>> EndAssignmentAsync(int id, int assignmentId, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        var assignment = staff?.Assignments.FirstOrDefault(a => a.Id == assignmentId && a.EndedOn == null);
        if (assignment == null) return Error.NotFound("Assignment");
        assignment.EndedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        assignment.EndReason = "Removed";
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<SectionDto>> SetClassTeacherAsync(int sectionId, ClassTeacherRequest request, CancellationToken ct = default)
    {
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (section == null) return Error.NotFound("Section");

        if (request.StaffId is int staffId)
        {
            var staff = await repositories.Staff.GetTrackedAsync(staffId, ct);
            if (staff == null || staff.CampusId != section.CampusId) return Error.Validation("Pick a teacher of this campus.");
            var error = CanTeach(staff);
            if (error != null) return error;
        }

        section.ClassTeacherStaffId = request.StaffId;
        await repositories.SaveChangesAsync(ct);
        return (await repositories.Sections.GetDtoAsync(sectionId, ct))!;
    }

    public async Task<Result<IReadOnlyList<ClassTeacherOptionDto>>> GetClassTeacherOptionsAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (section == null) return Error.NotFound("Section");
        return await repositories.Staff.TeacherOptionsAsync(section.CampusId, ct);
    }

    public async Task<Result<IReadOnlyList<OptionSubjectDto>>> GetSectionTeachersAsync(int sectionId, CancellationToken ct = default)
    {
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (section == null) return Error.NotFound("Section");
        return await repositories.Teaching.SectionTeachersAsync(section, ct);
    }

    private static Error? CanTeach(Staff staff) =>
        staff.Category != StaffCategory.Teaching ? Error.Validation("Only teaching staff can be given classes.")
        : !IsCurrent(staff.Status) ? Error.Validation("This staff member has left.")
        : null;
}
