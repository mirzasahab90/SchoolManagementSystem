using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Application.Services.Students;

public static class PhoneNormalizer
{
    /// <summary>Digits only; empty for null.</summary>
    public static string Digits(string? value) => value == null ? string.Empty : new string(value.Where(char.IsDigit).ToArray());
}

/// <summary>
/// Admission, profile and lifecycle of students. Students and enrollments are campus data (filtered and
/// guarded by the DbContext); guardians are school-wide and only exposed through visible students.
/// </summary>
public partial class StudentService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IStudentService
{
    public async Task<Result<PagedResult<StudentListItemDto>>> SearchAsync(
        string? q, int? classId, int? sectionId, StudentStatus? status, int? sessionId, DateOnly? admittedFrom, DateOnly? admittedTo,
        string? sort, string? dir, int page, int pageSize, CancellationToken ct = default) =>
        await repositories.Students.SearchAsync(q, classId, sectionId, status, sessionId, admittedFrom, admittedTo,
            sort, QueryOrdering.IsDescending(dir), Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);

    public async Task<Result<StudentProfileDto>> GetProfileAsync(int id, CancellationToken ct = default)
    {
        var detail = await repositories.Students.GetDetailAsync(id, ct);
        if (detail == null) return Error.NotFound("Student");

        var guardians = await repositories.Students.GetGuardiansAsync(id, ct);
        var current = await repositories.Enrollments.GetCurrentAsync(id, ct);
        var enrollment = current == null
            ? null
            : new EnrollmentDto(current.Id, current.AcademicSessionId, current.AcademicSession.Name, current.Section.ClassId,
                current.Section.Class.Name, current.SectionId, current.Section.Name, current.RollNo, current.Status,
                current.StartDate, current.EndDate);
        var history = await repositories.Enrollments.GetHistoryAsync(id, ct);
        var documents = await repositories.Students.GetDocumentsAsync(id, ct);
        var siblings = (await repositories.Students.GetSiblingsAsync(id, guardians.Select(g => g.GuardianId), ct))
            .Select(x => currentUser.CanAccessCampus(x.CampusId)
                ? x.Sibling
                : x.Sibling with { StudentId = null, ClassName = null, SectionName = null })
            .ToList();

        return new StudentProfileDto(detail, guardians, enrollment, history, documents, siblings);
    }

    public async Task<Result<StudentProfileDto>> AdmitAsync(AdmissionRequest request, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");

        // Section, session and campus.
        var section = await repositories.Sections.GetTrackedAsync(request.Enrollment.SectionId, ct);
        if (section == null || section.CampusId != campusId) return Error.Validation("Pick a section of this campus.");
        if (!section.IsActive) return Error.Validation("That section is inactive.");
        if (section.AcademicSession.Status == SessionStatus.Closed) return Error.Validation("This session is closed.");
        if (request.Enrollment.SessionId is int sid && sid != section.AcademicSessionId)
            return Error.Validation("The section belongs to another session.");

        var studentError = await ValidateStudentAsync(request.Student, null, ct);
        if (studentError != null) return studentError;

        var guardianError = ValidateGuardianSet(request.Guardians);
        if (guardianError != null) return guardianError;

        await using var tx = await repositories.BeginTransactionAsync(ct);

        var links = new List<(Guardian Guardian, GuardianLinkRequest Link)>();
        foreach (var link in request.Guardians)
        {
            var resolved = await ResolveGuardianAsync(link, ct);
            if (resolved.Error != null) return resolved.Error;
            if (links.Any(l => l.Guardian.Id != 0 && l.Guardian.Id == resolved.Guardian!.Id))
                return Error.Validation("A guardian is listed twice.");
            links.Add((resolved.Guardian!, link));
        }

        // Admission number: typed (must be free) or the next free generated one.
        var campusCode = (await repositories.Campuses.GetDtoAsync(campusId, ct))!.Code;
        string admissionNo;
        if (!string.IsNullOrWhiteSpace(request.AdmissionNo))
        {
            admissionNo = request.AdmissionNo.Trim().ToUpperInvariant();
            if (await repositories.Students.AdmissionNoTakenAsync(campusId, admissionNo, null, ct))
                return Error.Conflict($"Admission number {admissionNo} already exists.");
        }
        else
        {
            var year = request.Student.AdmissionDate.Year;
            do
            {
                var n = await repositories.Sequences.NextAsync(campusId, $"ADM-{year}", ct);
                admissionNo = $"{campusCode}-{year}-{n:0000}";
            } while (await repositories.Students.AdmissionNoTakenAsync(campusId, admissionNo, null, ct));
        }

        int rollNo;
        if (request.Enrollment.RollNo is int typedRoll)
        {
            if (typedRoll < 1) return Error.Validation("Roll number must be 1 or more.");
            if (await repositories.Enrollments.RollTakenAsync(section.Id, typedRoll, null, ct))
                return Error.Conflict($"Roll number {typedRoll} is taken in this section.");
            rollNo = typedRoll;
        }
        else
        {
            rollNo = await repositories.Enrollments.NextRollAsync(section.Id, ct);
        }

        var student = new Student { CampusId = campusId, AdmissionNo = admissionNo, Status = StudentStatus.Active };
        Apply(student, request.Student);
        foreach (var (guardian, link) in links)
        {
            student.Guardians.Add(new StudentGuardian
            {
                Guardian = guardian,
                Relation = link.Relation,
                IsPrimary = link.IsPrimary,
                IsEmergencyContact = link.IsEmergencyContact
            });
        }

        var enrollment = new Enrollment
        {
            CampusId = campusId,
            Student = student,
            AcademicSessionId = section.AcademicSessionId,
            SectionId = section.Id,
            RollNo = rollNo,
            Status = EnrollmentStatus.Active,
            StartDate = student.AdmissionDate
        };
        repositories.Students.Add(student);
        repositories.Students.Add(enrollment);
        await repositories.SaveChangesAsync(ct);

        repositories.Enrollments.AddHistory(new EnrollmentHistory
        {
            EnrollmentId = enrollment.Id,
            StudentId = student.Id,
            Action = EnrollmentAction.Admitted,
            ToSectionId = section.Id,
            OccurredOn = DateTime.UtcNow,
            ByUserId = currentUser.UserId
        });
        await repositories.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await GetProfileAsync(student.Id, ct);
    }

    public async Task<Result<StudentProfileDto>> UpdateAsync(int id, SaveStudentRequest request, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(id, ct);
        if (student == null) return Error.NotFound("Student");

        var error = await ValidateStudentAsync(request, id, ct);
        if (error != null) return error;

        Apply(student, request);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<StudentProfileDto>> AddGuardianAsync(int studentId, GuardianLinkRequest request, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return Error.NotFound("Student");

        var resolved = await ResolveGuardianAsync(request, ct);
        if (resolved.Error != null) return resolved.Error;
        var guardian = resolved.Guardian!;
        if (guardian.Id != 0 && student.Guardians.Any(g => g.GuardianId == guardian.Id))
            return Error.Conflict("This guardian is already linked to the student.");

        if (request.IsPrimary)
        {
            foreach (var link in student.Guardians) link.IsPrimary = false;
        }

        student.Guardians.Add(new StudentGuardian
        {
            Guardian = guardian,
            Relation = request.Relation,
            IsPrimary = request.IsPrimary || student.Guardians.Count == 0,
            IsEmergencyContact = request.IsEmergencyContact
        });
        student.ModifiedOn = DateTime.UtcNow; // mark the student modified so the campus save guard checks this write
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(studentId, ct);
    }

    public async Task<Result<StudentProfileDto>> UpdateGuardianLinkAsync(int studentId, int guardianId, UpdateGuardianLinkRequest request, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return Error.NotFound("Student");

        var link = student.Guardians.FirstOrDefault(g => g.GuardianId == guardianId);
        if (link == null) return Error.NotFound("Guardian");
        if (link.IsPrimary && !request.IsPrimary) return Error.Validation("Choose another primary guardian first.");

        if (request.IsPrimary)
        {
            foreach (var other in student.Guardians) other.IsPrimary = other.GuardianId == guardianId;
        }

        link.IsEmergencyContact = request.IsEmergencyContact;
        student.ModifiedOn = DateTime.UtcNow;
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(studentId, ct);
    }

    public async Task<Result<StudentProfileDto>> RemoveGuardianAsync(int studentId, int guardianId, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return Error.NotFound("Student");

        var link = student.Guardians.FirstOrDefault(g => g.GuardianId == guardianId);
        if (link == null) return Error.NotFound("Guardian");
        if (student.Guardians.Count == 1) return Error.Validation("A student must keep at least one guardian.");
        if (link.IsPrimary) return Error.Validation("Choose another primary guardian first.");

        student.Guardians.Remove(link);
        student.ModifiedOn = DateTime.UtcNow;
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(studentId, ct);
    }

    private async Task<Error?> ValidateStudentAsync(SaveStudentRequest s, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(s.FirstName) || string.IsNullOrWhiteSpace(s.LastName))
            return Error.Validation("First and last name are required.");
        if (s.DateOfBirth >= DateOnly.FromDateTime(DateTime.UtcNow))
            return Error.Validation("Date of birth must be in the past.");
        var bform = PhoneNormalizer.Digits(s.BFormNo);
        if (bform.Length > 0 && await repositories.Students.BFormTakenAsync(bform, excludeId, ct))
            return Error.Conflict("A student with this B-Form number already exists.");
        return null;
    }

    private static Error? ValidateGuardianSet(IReadOnlyList<GuardianLinkRequest> guardians)
    {
        if (guardians.Count == 0) return Error.Validation("A student must keep at least one guardian.");
        if (guardians.Count(g => g.IsPrimary) != 1) return Error.Validation("Choose exactly one primary guardian.");
        return null;
    }

    private sealed record ResolvedGuardian(Error? Error, Guardian? Guardian);

    /// <summary>
    /// An existing guardian (by id) or a new one. Linking a guardian the caller cannot already see requires
    /// the matching CNIC or phone, so guessing an id never reveals someone else's details.
    /// </summary>
    private async Task<ResolvedGuardian> ResolveGuardianAsync(GuardianLinkRequest link, CancellationToken ct)
    {
        if (link.GuardianId is int gid)
        {
            var existing = await repositories.Guardians.GetTrackedAsync(gid, ct);
            if (existing == null) return new(Error.NotFound("Guardian"), null);
            if (!currentUser.IsHq && !await repositories.Guardians.IsVisibleAsync(gid, ct))
            {
                var cnic = PhoneNormalizer.Digits(link.NewGuardian?.Cnic);
                var phone = PhoneNormalizer.Digits(link.NewGuardian?.Phone);
                var proven = (cnic.Length > 0 && cnic == existing.Cnic)
                             || (phone.Length > 0 && phone == PhoneNormalizer.Digits(existing.Phone));
                if (!proven) return new(Error.NotFound("Guardian"), null);
            }

            return new(null, existing);
        }

        var g = link.NewGuardian;
        if (g == null) return new(Error.Validation("Each guardian needs an existing guardian or new details."), null);
        if (string.IsNullOrWhiteSpace(g.FullName)) return new(Error.Validation("Guardian name is required."), null);
        if (PhoneNormalizer.Digits(g.Phone).Length < 7) return new(Error.Validation("Guardian phone is required."), null);

        var cnicDigits = PhoneNormalizer.Digits(g.Cnic);
        if (cnicDigits.Length > 0 && await repositories.Guardians.FindByCnicAsync(cnicDigits, ct) != null)
            return new(Error.Conflict("A guardian with this CNIC exists — link them instead."), null);

        var guardian = new Guardian();
        ApplyGuardian(guardian, g);
        return new(null, guardian);
    }

    internal static void ApplyGuardian(Guardian guardian, SaveGuardianRequest g)
    {
        guardian.FullName = g.FullName.Trim();
        var cnic = PhoneNormalizer.Digits(g.Cnic);
        guardian.Cnic = cnic.Length > 0 ? cnic : null;
        guardian.Phone = g.Phone.Trim();
        guardian.AltPhone = Clean(g.AltPhone);
        guardian.Email = Clean(g.Email);
        guardian.Occupation = Clean(g.Occupation);
        guardian.Address = Clean(g.Address);
    }

    private static void Apply(Student student, SaveStudentRequest s)
    {
        student.FirstName = s.FirstName.Trim();
        student.LastName = s.LastName.Trim();
        student.Gender = s.Gender;
        student.DateOfBirth = s.DateOfBirth;
        var bform = PhoneNormalizer.Digits(s.BFormNo);
        student.BFormNo = bform.Length > 0 ? bform : null;
        student.BloodGroup = Clean(s.BloodGroup);
        student.Religion = Clean(s.Religion);
        student.Nationality = Clean(s.Nationality);
        student.Address = Clean(s.Address);
        student.Phone = Clean(s.Phone);
        student.AdmissionDate = s.AdmissionDate;
        student.PreviousSchool = Clean(s.PreviousSchool);
        student.Notes = Clean(s.Notes);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
