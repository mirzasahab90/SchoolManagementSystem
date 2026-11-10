using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Fees;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>Per-student concessions (manual and staff child), staff-child suggestions and a line preview.</summary>
public class ConcessionService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser, ChargeResolver charges)
    : IConcessionService
{
    public async Task<Result<PagedResult<ConcessionDto>>> ListAsync(int sessionId, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId == null && !currentUser.IsHq) return Error.Validation("Select a campus.");
        return await repositories.Concessions.ListAsync(sessionId, q, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
    }

    public async Task<Result<ConcessionDto>> CreateAsync(SaveConcessionRequest r, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(r.StudentId, ct);
        if (student == null) return Error.NotFound("Student");
        var row = new StudentConcession { CampusId = student.CampusId, StudentId = student.Id };
        var error = await ApplyAsync(row, r, null, student.CampusId, ct);
        if (error != null) return error;
        repositories.Concessions.Add(row);
        await repositories.SaveChangesAsync(ct);
        return await DtoAsync(row, ct);
    }

    public async Task<Result<ConcessionDto>> UpdateAsync(int id, SaveConcessionRequest r, CancellationToken ct = default)
    {
        var row = await repositories.Concessions.GetTrackedAsync(id, ct);
        if (row == null) return Error.NotFound("Concession");
        if (r.StudentId != row.StudentId) return Error.Validation("A concession cannot move to another student.");
        var error = await ApplyAsync(row, r, id, row.CampusId, ct);
        if (error != null) return error;
        await repositories.SaveChangesAsync(ct);
        return await DtoAsync(row, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var row = await repositories.Concessions.GetTrackedAsync(id, ct);
        if (row == null) return Error.NotFound("Concession");
        row.IsDeleted = true;
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Concession removed.");
    }

    public async Task<Result<IReadOnlyList<StaffChildSuggestionDto>>> StaffChildSuggestionsAsync(CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct);
        List<StaffChildSuggestionDto> list = session == null ? [] : await repositories.Concessions.StaffChildSuggestionsAsync(campusId, session.Id, ct);
        return list;
    }

    /// <summary>Head lines the student would get for a month with the current structure and concessions.</summary>
    public async Task<Result<IReadOnlyList<LineCalc>>> PreviewAsync(int studentId, DateOnly month, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return Error.NotFound("Student");
        var enrollment = await repositories.Enrollments.GetCurrentAsync(studentId, ct);
        if (enrollment == null) return Error.Validation("The student has no enrollment.");
        var first = new DateOnly(month.Year, month.Month, 1);
        var lines = await charges.LinesAsync(student.Id, student.CampusId, student.AdmissionDate, enrollment.AcademicSessionId,
            enrollment.Section.ClassId, first, enrollment.AcademicSession, ct);
        return lines.ToList();
    }

    private async Task<Error?> ApplyAsync(StudentConcession row, SaveConcessionRequest r, int? id, int campusId, CancellationToken ct)
    {
        var reason = r.Reason?.Trim() ?? "";
        if (reason.Length is 0 or > 200) return Error.Validation("Give a reason (200 characters at most).");
        if (await repositories.Sessions.GetTrackedAsync(r.SessionId, ct) == null) return Error.NotFound("Session");
        if (r.FeeHeadId is int headId && (await repositories.FeeSetup.HeadsAsync(campusId, ct)).All(h => h.Id != headId))
            return Error.NotFound("Fee head");

        var value = r.Value;
        if (r.Kind == ConcessionKind.StaffChild)
        {
            if (r.StaffId is not int staffId) return Error.Validation("Pick the staff member.");
            var staff = await repositories.Staff.GetTrackedAsync(staffId, ct);
            if (staff == null || staff.CampusId != campusId) return Error.NotFound("Staff member");
            if (staff.Status is StaffStatus.Resigned or StaffStatus.Terminated) return Error.Validation("This staff member has left.");
            value ??= (await charges.SettingsAsync(campusId, ct)).StaffChildPercent;
        }
        else if (r.StaffId != null) return Error.Validation("Only a staff-child concession links a staff member.");

        if (value is not decimal v || v <= 0) return Error.Validation("Enter a value above 0.");
        if (r.ValueType == ConcessionValueType.Percent && v > 100) return Error.Validation("A percentage cannot be more than 100.");
        if (v > 10_000_000) return Error.Validation("The amount is too large.");
        if (r.IsActive && await repositories.Concessions.DuplicateAsync(row.StudentId, r.SessionId, r.FeeHeadId, r.Kind, id, ct))
            return Error.Conflict("This student already has an active concession of this kind for this head.");

        row.AcademicSessionId = r.SessionId;
        row.FeeHeadId = r.FeeHeadId;
        row.Kind = r.Kind;
        row.StaffId = r.Kind == ConcessionKind.StaffChild ? r.StaffId : null;
        row.ValueType = r.ValueType;
        row.Value = decimal.Round(v, 2);
        row.Reason = reason;
        row.IsActive = r.IsActive;
        return null;
    }

    private async Task<ConcessionDto> DtoAsync(StudentConcession row, CancellationToken ct)
    {
        var page = await repositories.Concessions.ListAsync(row.AcademicSessionId, null, 1, 100_000, ct);
        return page.Items.First(x => x.Id == row.Id);
    }
}
