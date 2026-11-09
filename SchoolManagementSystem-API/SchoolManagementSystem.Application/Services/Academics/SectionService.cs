using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Application.Services.Academics;

/// <summary>
/// Sections of a class on one campus for one session. Their class and session must be HQ-owned or owned by
/// the same campus, and a Closed session is read-only.
/// </summary>
public class SectionService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : ISectionService
{
    private static readonly Error Closed = Error.Validation("This session is closed.");

    public async Task<Result<IReadOnlyList<SectionDto>>> ListAsync(int? sessionId, int? classId, CancellationToken ct = default)
    {
        var session = sessionId ?? (await repositories.Sessions.GetEffectiveCurrentAsync(currentUser.ActiveCampusId, ct))?.Id;
        if (session == null) return new List<SectionDto>();
        return await repositories.Sections.ListAsync(session.Value, classId, ct);
    }

    public async Task<Result<SectionDto>> CreateAsync(SaveSectionRequest request, CancellationToken ct = default)
    {
        var campusId = request.CampusId ?? currentUser.ActiveCampusId;
        if (campusId == null) return Error.Validation("Select a campus.");
        if (!currentUser.CanAccessCampus(campusId.Value)) return Error.Forbidden("You do not have access to this campus.");

        var resolved = await ResolveAsync(request, campusId.Value, null, ct);
        if (resolved.Error != null) return resolved.Error;

        var section = new Section { CampusId = campusId.Value, AcademicSessionId = resolved.SessionId };
        Apply(section, request);
        repositories.Sections.Add(section);
        await repositories.SaveChangesAsync(ct);
        return await GetDtoAsync(section.Id, ct);
    }

    public async Task<Result<SectionDto>> UpdateAsync(int id, SaveSectionRequest request, CancellationToken ct = default)
    {
        var section = await repositories.Sections.GetTrackedAsync(id, ct);
        if (section == null) return Error.NotFound("Section");
        if (section.AcademicSession.Status == SessionStatus.Closed) return Closed;

        // A section stays on its campus and session; only class and details change.
        var resolved = await ResolveAsync(request with { AcademicSessionId = section.AcademicSessionId }, section.CampusId, id, ct);
        if (resolved.Error != null) return resolved.Error;

        Apply(section, request);
        await repositories.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var section = await repositories.Sections.GetTrackedAsync(id, ct);
        if (section == null) return Error.NotFound("Section");
        if (section.AcademicSession.Status == SessionStatus.Closed) return Closed;

        repositories.Sections.Remove(section);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Section deleted.");
    }

    public async Task<Result<CopySectionsResultDto>> CopyAsync(CopySectionsRequest request, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        if (request.FromSessionId == request.ToSessionId) return Error.Validation("Pick a different session to copy into.");

        var from = await repositories.Sessions.GetTrackedAsync(request.FromSessionId, ct);
        var to = await repositories.Sessions.GetTrackedAsync(request.ToSessionId, ct);
        if (from == null || to == null) return Error.NotFound("Session");
        if (!Ownership.CanReference(to.CampusId, campusId)) return Error.Validation("That session belongs to another campus.");
        if (to.Status == SessionStatus.Closed) return Closed;

        var source = await repositories.Sections.ListForCopyAsync(campusId, from.Id, ct);
        int copied = 0, skipped = 0;
        foreach (var s in source)
        {
            if (await repositories.Sections.ExistsAsync(campusId, to.Id, s.ClassId, s.Name, null, ct))
            {
                skipped++;
                continue;
            }

            copied++;
            if (request.DryRun) continue;
            repositories.Sections.Add(new Section
            {
                CampusId = campusId,
                AcademicSessionId = to.Id,
                ClassId = s.ClassId,
                Name = s.Name,
                Shift = s.Shift,
                Capacity = s.Capacity,
                Room = s.Room,
                IsActive = true
            });
        }

        if (!request.DryRun) await repositories.SaveChangesAsync(ct);
        return new CopySectionsResultDto(copied, skipped);
    }

    private sealed record Resolved(Error? Error, int SessionId);

    /// <summary>Validates session, class and name for a section on <paramref name="campusId"/>.</summary>
    private async Task<Resolved> ResolveAsync(SaveSectionRequest request, int campusId, int? excludeId, CancellationToken ct)
    {
        AcademicSession? session = request.AcademicSessionId is int sid
            ? await repositories.Sessions.GetTrackedAsync(sid, ct)
            : await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct);
        if (session == null) return new(request.AcademicSessionId == null ? Error.Validation("No current session.") : Error.NotFound("Session"), 0);
        if (!Ownership.CanReference(session.CampusId, campusId)) return new(Error.Validation("That session belongs to another campus."), 0);
        if (session.Status == SessionStatus.Closed) return new(Closed, 0);

        var schoolClass = await repositories.Classes.GetForSectionAsync(request.ClassId, ct);
        if (schoolClass == null || !Ownership.CanReference(schoolClass.CampusId, campusId))
            return new(Error.Validation("That class cannot be used on this campus."), 0);
        if (!schoolClass.IsActive) return new(Error.Validation("Class is inactive."), 0);

        if (string.IsNullOrWhiteSpace(request.Name)) return new(Error.Validation("Section name is required."), 0);
        if (request.Capacity is < 0) return new(Error.Validation("Capacity cannot be negative."), 0);

        var name = request.Name.Trim();
        if (await repositories.Sections.ExistsAsync(campusId, session.Id, schoolClass.Id, name, excludeId, ct))
            return new(Error.Conflict($"Section {schoolClass.Name} {name} already exists in this session."), 0);

        return new(null, session.Id);
    }

    private static void Apply(Section section, SaveSectionRequest request)
    {
        section.ClassId = request.ClassId;
        section.Name = request.Name.Trim();
        section.Shift = request.Shift;
        section.Capacity = request.Capacity;
        section.Room = string.IsNullOrWhiteSpace(request.Room) ? null : request.Room.Trim();
        section.IsActive = request.IsActive;
    }

    private async Task<Result<SectionDto>> GetDtoAsync(int id, CancellationToken ct)
    {
        var dto = await repositories.Sections.GetDtoAsync(id, ct);
        return dto is null ? Error.NotFound("Section") : dto;
    }
}
