using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Application.Services.Students;

/// <summary>Section change, leave and rejoin. Every change writes an EnrollmentHistory line.</summary>
public partial class StudentService
{
    public async Task<Result<StudentProfileDto>> ChangeSectionAsync(int id, ChangeSectionRequest request, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(id, ct);
        if (student == null) return Error.NotFound("Student");
        if (student.Status == StudentStatus.Left) return Error.Validation("Rejoin the student first.");

        var enrollment = await repositories.Enrollments.GetCurrentAsync(id, ct);
        if (enrollment == null || enrollment.Status != EnrollmentStatus.Active) return Error.Validation("The student has no active enrollment.");

        var target = await repositories.Sections.GetTrackedAsync(request.SectionId, ct);
        if (target == null || target.CampusId != student.CampusId || target.AcademicSessionId != enrollment.AcademicSessionId)
            return Error.Validation("Pick a section of the same campus and session.");
        if (!target.IsActive) return Error.Validation("That section is inactive.");
        if (target.AcademicSession.Status == SessionStatus.Closed) return Error.Validation("This session is closed.");
        if (target.Id == enrollment.SectionId) return Error.Validation("The student is already in that section.");

        var roll = await PickRollAsync(target.Id, request.RollNo, enrollment.Id, ct);
        if (roll.Error != null) return roll.Error;

        var from = enrollment.SectionId;
        enrollment.SectionId = target.Id;
        enrollment.RollNo = roll.Value;
        AddHistory(enrollment, EnrollmentAction.SectionChanged, from, target.Id, request.Reason);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<StudentProfileDto>> LeaveAsync(int id, LeaveRequest request, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(id, ct);
        if (student == null) return Error.NotFound("Student");
        if (student.Status == StudentStatus.Left) return Error.Validation("The student has already left.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return Error.Validation("A reason is required.");
        if (request.LeftOn < student.AdmissionDate) return Error.Validation("The leaving date is before the admission date.");

        var enrollment = await repositories.Enrollments.GetCurrentAsync(id, ct);
        student.Status = StudentStatus.Left;
        student.LeftOn = request.LeftOn;
        student.LeaveReason = request.Reason.Trim();
        if (enrollment != null && enrollment.Status == EnrollmentStatus.Active)
        {
            enrollment.Status = EnrollmentStatus.Left;
            enrollment.EndDate = request.LeftOn;
            AddHistory(enrollment, EnrollmentAction.Left, enrollment.SectionId, null, student.LeaveReason);
        }

        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<StudentProfileDto>> RejoinAsync(int id, RejoinRequest request, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(id, ct);
        if (student == null) return Error.NotFound("Student");
        if (student.Status != StudentStatus.Left) return Error.Validation("Only a student who has left can rejoin.");

        var last = await repositories.Enrollments.GetCurrentAsync(id, ct);
        var effective = await repositories.Sessions.GetEffectiveCurrentAsync(student.CampusId, ct);

        // Target: the given section, or the old one if it is still usable in the current session.
        Section? target;
        if (request.SectionId is int sectionId)
        {
            target = await repositories.Sections.GetTrackedAsync(sectionId, ct);
            if (target == null || target.CampusId != student.CampusId) return Error.Validation("Pick a section of this campus.");
        }
        else
        {
            target = last == null ? null : await repositories.Sections.GetTrackedAsync(last.SectionId, ct);
            var usable = target != null && target.IsActive && effective != null && target.AcademicSessionId == effective.Id;
            if (!usable) return Error.Validation("Pick a section.");
        }

        if (!target!.IsActive) return Error.Validation("That section is inactive.");
        if (target.AcademicSession.Status == SessionStatus.Closed) return Error.Validation("This session is closed.");

        // Same session: reactivate that enrollment (moving it if needed). Otherwise open a new one.
        var enrollment = await repositories.Enrollments.GetForSessionAsync(id, target.AcademicSessionId, ct);
        int? keepRoll = null;
        if (request.RollNo == null && enrollment?.SectionId == target.Id
            && !await repositories.Enrollments.RollTakenAsync(target.Id, enrollment.RollNo, enrollment.Id, ct))
        {
            keepRoll = enrollment.RollNo; // the old roll is still free, so keep it
        }

        var roll = await PickRollAsync(target.Id, request.RollNo ?? keepRoll, enrollment?.Id, ct);
        if (roll.Error != null) return roll.Error;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = enrollment?.SectionId;
        if (enrollment == null)
        {
            enrollment = new Enrollment
            {
                CampusId = student.CampusId,
                StudentId = student.Id,
                AcademicSessionId = target.AcademicSessionId,
                StartDate = today
            };
            repositories.Students.Add(enrollment);
        }

        enrollment.SectionId = target.Id;
        enrollment.RollNo = roll.Value;
        enrollment.Status = EnrollmentStatus.Active;
        enrollment.EndDate = null;
        student.Status = StudentStatus.Active;
        student.LeftOn = null;
        student.LeaveReason = null;
        await repositories.SaveChangesAsync(ct);

        AddHistory(enrollment, EnrollmentAction.Rejoined, from, target.Id, null);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedWithChildrenAsync(id, ct);
        if (student == null) return Error.NotFound("Student");

        foreach (var e in student.Enrollments) repositories.Students.Remove(e);
        foreach (var d in student.Documents) repositories.Students.Remove(d);
        repositories.Students.Remove(student);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Student deleted.");
    }

    private sealed record RollPick(Error? Error, int Value);

    private async Task<RollPick> PickRollAsync(int sectionId, int? requested, int? enrollmentId, CancellationToken ct)
    {
        if (requested is int roll)
        {
            if (roll < 1) return new(Error.Validation("Roll number must be 1 or more."), 0);
            if (await repositories.Enrollments.RollTakenAsync(sectionId, roll, enrollmentId, ct))
                return new(Error.Conflict($"Roll number {roll} is taken in this section."), 0);
            return new(null, roll);
        }

        return new(null, await repositories.Enrollments.NextRollAsync(sectionId, ct));
    }

    private void AddHistory(Enrollment enrollment, EnrollmentAction action, int? fromSection, int? toSection, string? reason) =>
        repositories.Enrollments.AddHistory(new EnrollmentHistory
        {
            EnrollmentId = enrollment.Id,
            StudentId = enrollment.StudentId,
            Action = action,
            FromSectionId = fromSection,
            ToSectionId = toSection,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OccurredOn = DateTime.UtcNow,
            ByUserId = currentUser.UserId
        });
}
