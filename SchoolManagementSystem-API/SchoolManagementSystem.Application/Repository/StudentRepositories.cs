using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Students;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

/// <summary>Per-campus counters (admission numbers). Must be called inside the caller's transaction.</summary>
public class SequenceRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>Atomically returns the next value for (campus, key), starting at 1.</summary>
    public async Task<int> NextAsync(int campusId, string key, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var taken = await Db.Database
                .SqlQuery<int>($"UPDATE CampusSequences SET NextValue = NextValue + 1 OUTPUT deleted.NextValue AS [Value] WHERE CampusId = {campusId} AND [Key] = {key}")
                .ToListAsync(ct);
            if (taken.Count == 1)
            {
                return taken[0];
            }

            try
            {
                await Db.Database.ExecuteSqlAsync($"INSERT INTO CampusSequences (CampusId, [Key], NextValue) VALUES ({campusId}, {key}, 2)", ct);
                return 1;
            }
            catch (SqlException ex) when (ex.Number is 2627 or 2601)
            {
                // Another admission created the row first; take a number from it instead.
            }
        }

        throw new InvalidOperationException("Could not allocate an admission number.");
    }
}

public class StudentRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>
    /// Students of the request's campuses, searched and paged. <paramref name="sort"/> is one of name, admissionNo,
    /// admitted, class, roll (class and roll use the active enrollment); anything else keeps the default name order.
    /// </summary>
    public async Task<PagedResult<StudentListItemDto>> SearchAsync(
        string? q, int? classId, int? sectionId, StudentStatus? status, int? sessionId, DateOnly? admittedFrom, DateOnly? admittedTo,
        string? sort, bool descending, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Db.Students.AsNoTracking().AsQueryable();
        if (status != null) query = query.Where(s => s.Status == status);
        if (sessionId != null) query = query.Where(s => s.Enrollments.Any(e => e.AcademicSessionId == sessionId));
        if (admittedFrom != null) query = query.Where(s => s.AdmissionDate >= admittedFrom);
        if (admittedTo != null) query = query.Where(s => s.AdmissionDate <= admittedTo);
        if (sectionId != null) query = query.Where(s => s.Enrollments.Any(e => e.Status == EnrollmentStatus.Active && e.SectionId == sectionId));
        if (classId != null) query = query.Where(s => s.Enrollments.Any(e => e.Status == EnrollmentStatus.Active && e.Section.ClassId == classId));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            var digits = new string(text.Where(char.IsDigit).ToArray());
            query = query.Where(s =>
                (s.FirstName + " " + s.LastName).Contains(text)
                || s.AdmissionNo.Contains(text)
                || (digits.Length >= 4 && s.BFormNo != null && s.BFormNo.Contains(digits))
                || (digits.Length >= 4 && s.Guardians.Any(g =>
                    g.Guardian.Phone.Replace("-", "").Replace(" ", "").Replace("+", "").Replace("(", "").Replace(")", "").Contains(digits)
                    || (g.Guardian.Cnic != null && g.Guardian.Cnic.Contains(digits)))));
        }

        var total = await query.CountAsync(ct);
        var items = await Sort(query, sort, descending)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.AdmissionNo, s.FirstName, s.LastName, s.Gender, HasPhoto = s.PhotoPath != null, s.Status,
                CampusCode = s.Campus.Code,
                Enrollment = s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active)
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => new { ClassName = e.Section.Class.Name, SectionName = e.Section.Name, e.RollNo })
                    .FirstOrDefault(),
                Guardian = s.Guardians.Where(g => g.IsPrimary).Select(g => new { g.Guardian.FullName, g.Guardian.Phone }).FirstOrDefault()
            })
            .ToListAsync(ct);

        return new PagedResult<StudentListItemDto>(items.Select(x => new StudentListItemDto(
            x.Id, x.AdmissionNo, $"{x.FirstName} {x.LastName}".Trim(), x.Gender, x.HasPhoto,
            x.Enrollment?.ClassName, x.Enrollment?.SectionName, x.Enrollment?.RollNo,
            x.Guardian?.FullName, x.Guardian?.Phone, x.Status, x.CampusCode)).ToList(), total);
    }

    private static IOrderedQueryable<Student> Sort(IQueryable<Student> query, string? sort, bool desc)
    {
        switch (sort?.Trim().ToLowerInvariant())
        {
            case "name":
                return query.OrderByDir(s => s.FirstName, desc).ThenByDir(s => s.LastName, desc).ThenByDir(s => s.Id, desc);
            case "admissionno":
                return query.OrderByDir(s => s.AdmissionNo, desc).ThenByDir(s => s.Id, desc);
            case "admitted":
                return query.OrderByDir(s => s.AdmissionDate, desc).ThenByDir(s => s.Id, desc);
            case "class":
                return query
                    .OrderByDir(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).OrderByDescending(e => e.StartDate)
                        .Select(e => (int?)e.Section.Class.DisplayOrder).FirstOrDefault(), desc)
                    .ThenByDir(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).OrderByDescending(e => e.StartDate)
                        .Select(e => e.Section.Name).FirstOrDefault(), desc)
                    .ThenByDir(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).OrderByDescending(e => e.StartDate)
                        .Select(e => (int?)e.RollNo).FirstOrDefault(), desc)
                    .ThenByDir(s => s.Id, desc);
            case "roll":
                return query
                    .OrderByDir(s => s.Enrollments.Where(e => e.Status == EnrollmentStatus.Active).OrderByDescending(e => e.StartDate)
                        .Select(e => (int?)e.RollNo).FirstOrDefault(), desc)
                    .ThenByDir(s => s.FirstName, desc).ThenByDir(s => s.LastName, desc).ThenByDir(s => s.Id, desc);
            default:
                // Unknown or missing sort: the original order (dir is ignored too).
                return query.OrderBy(s => s.FirstName).ThenBy(s => s.LastName);
        }
    }

    public Task<Student?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Students.Include(s => s.Campus).Include(s => s.Guardians).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Student?> GetTrackedWithChildrenAsync(int id, CancellationToken ct = default) =>
        Db.Students.Include(s => s.Enrollments).Include(s => s.Documents).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> AdmissionNoTakenAsync(int campusId, string admissionNo, int? excludeId, CancellationToken ct = default) =>
        Db.Students.IgnoreQueryFilters().AnyAsync(s =>
            !s.IsDeleted && s.CampusId == campusId && s.AdmissionNo == admissionNo && (excludeId == null || s.Id != excludeId), ct);

    public Task<bool> BFormTakenAsync(string bform, int? excludeId, CancellationToken ct = default) =>
        Db.Students.IgnoreQueryFilters().AnyAsync(s =>
            !s.IsDeleted && s.BFormNo == bform && (excludeId == null || s.Id != excludeId), ct);

    public Task<StudentDetailDto?> GetDetailAsync(int id, CancellationToken ct = default) =>
        Db.Students.AsNoTracking().Where(s => s.Id == id).Select(s => new StudentDetailDto(
            s.Id, s.CampusId, s.Campus.Code, s.AdmissionNo, s.FirstName, s.LastName, s.FirstName + " " + s.LastName,
            s.Gender, s.DateOfBirth, s.BFormNo, s.BloodGroup, s.Religion, s.Nationality, s.Address, s.Phone,
            s.AdmissionDate, s.PreviousSchool, s.Notes, s.PhotoPath != null, s.Status, s.LeftOn, s.LeaveReason))
            .FirstOrDefaultAsync(ct);

    public Task<List<StudentGuardianDto>> GetGuardiansAsync(int studentId, CancellationToken ct = default) =>
        Db.StudentGuardians.AsNoTracking()
            .Where(x => x.StudentId == studentId && !x.Guardian.IsDeleted)
            .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.Guardian.FullName)
            .Select(x => new StudentGuardianDto(x.GuardianId, x.Guardian.FullName, x.Guardian.Cnic, x.Guardian.Phone,
                x.Guardian.AltPhone, x.Guardian.Email, x.Guardian.Occupation, x.Relation, x.IsPrimary, x.IsEmergencyContact))
            .ToListAsync(ct);

    public Task<List<StudentDocumentDto>> GetDocumentsAsync(int studentId, CancellationToken ct = default) =>
        Db.StudentDocuments.AsNoTracking()
            .Where(d => d.StudentId == studentId)
            .OrderByDescending(d => d.UploadedOn)
            .Select(d => new StudentDocumentDto(d.Id, d.Type, d.FileName, d.ContentType, d.SizeBytes, d.UploadedOn))
            .ToListAsync(ct);

    /// <summary>
    /// Other students sharing any guardian, on any campus (filters ignored). Rows on campuses the caller
    /// cannot access are returned with their campus id so the service can hide the student id.
    /// </summary>
    public async Task<List<(int StudentId, int CampusId, SiblingDto Sibling)>> GetSiblingsAsync(int studentId, IEnumerable<int> guardianIds, CancellationToken ct = default)
    {
        var ids = guardianIds.ToList();
        var rows = await Db.Students.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.Id != studentId && s.Guardians.Any(g => ids.Contains(g.GuardianId)))
            .OrderBy(s => s.FirstName)
            .Select(s => new
            {
                s.Id, s.CampusId, Name = s.FirstName + " " + s.LastName, CampusCode = s.Campus.Code,
                Current = s.Enrollments.Where(e => !e.IsDeleted && e.Status == EnrollmentStatus.Active)
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => new { ClassName = e.Section.Class.Name, SectionName = e.Section.Name })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
        return rows.Select(r => (r.Id, r.CampusId, new SiblingDto(r.Id, r.Name, r.CampusCode, r.Current?.ClassName, r.Current?.SectionName))).ToList();
    }

    public Task<int> CountActiveAsync(CancellationToken ct = default) =>
        Db.Students.CountAsync(s => s.Status == StudentStatus.Active, ct);

    public Task<int> CountAdmittedSinceAsync(DateOnly since, CancellationToken ct = default) =>
        Db.Students.CountAsync(s => s.AdmissionDate >= since, ct);
}

public class EnrollmentRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>The latest Active enrollment, else the latest of any status.</summary>
    public Task<Enrollment?> GetCurrentAsync(int studentId, CancellationToken ct = default) =>
        Db.Enrollments
            .Include(e => e.Section).ThenInclude(s => s.Class)
            .Include(e => e.AcademicSession)
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.Status == EnrollmentStatus.Active).ThenByDescending(e => e.StartDate).ThenByDescending(e => e.Id)
            .FirstOrDefaultAsync(ct);

    public Task<Enrollment?> GetForSessionAsync(int studentId, int sessionId, CancellationToken ct = default) =>
        Db.Enrollments.FirstOrDefaultAsync(e => e.StudentId == studentId && e.AcademicSessionId == sessionId, ct);

    public Task<bool> RollTakenAsync(int sectionId, int rollNo, int? excludeEnrollmentId, CancellationToken ct = default) =>
        Db.Enrollments.IgnoreQueryFilters().AnyAsync(e =>
            !e.IsDeleted && e.SectionId == sectionId && e.RollNo == rollNo && e.Status == EnrollmentStatus.Active
            && (excludeEnrollmentId == null || e.Id != excludeEnrollmentId), ct);

    public async Task<int> NextRollAsync(int sectionId, CancellationToken ct = default) =>
        (await Db.Enrollments.IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && e.SectionId == sectionId && e.Status == EnrollmentStatus.Active)
            .MaxAsync(e => (int?)e.RollNo, ct) ?? 0) + 1;

    public void AddHistory(EnrollmentHistory row) => Db.EnrollmentHistory.Add(row);

    public Task<List<EnrollmentHistoryDto>> GetHistoryAsync(int studentId, CancellationToken ct = default) =>
        Db.EnrollmentHistory.AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderBy(h => h.OccurredOn).ThenBy(h => h.Id)
            .Select(h => new EnrollmentHistoryDto(
                h.Id, h.Action,
                Db.Sections.IgnoreQueryFilters().Where(s => s.Id == h.FromSectionId).Select(s => s.Class.Name + " " + s.Name).FirstOrDefault(),
                Db.Sections.IgnoreQueryFilters().Where(s => s.Id == h.ToSectionId).Select(s => s.Class.Name + " " + s.Name).FirstOrDefault(),
                h.Reason, h.OccurredOn,
                Db.Users.IgnoreQueryFilters().Where(u => u.Id == h.ByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()))
            .ToListAsync(ct);
}

public class GuardianRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<Guardian?> FindByCnicAsync(string cnicDigits, CancellationToken ct = default) =>
        Db.Guardians.IgnoreQueryFilters().FirstOrDefaultAsync(g => !g.IsDeleted && g.Cnic == cnicDigits, ct);

    /// <summary>Any non-deleted guardian; the service decides whether the caller may see it.</summary>
    public Task<Guardian?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Guardians.IgnoreQueryFilters().FirstOrDefaultAsync(g => !g.IsDeleted && g.Id == id, ct);

    /// <summary>Guardians linked to at least one student visible to the request.</summary>
    public async Task<PagedResult<GuardianListItemDto>> SearchVisibleAsync(string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var visibleIds = Db.Students.SelectMany(s => s.Guardians.Select(g => g.GuardianId));
        var query = Db.Guardians.AsNoTracking().Where(g => visibleIds.Contains(g.Id));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            var digits = new string(text.Where(char.IsDigit).ToArray());
            query = query.Where(g => g.FullName.Contains(text)
                                     || (digits.Length >= 4 && (g.Phone.Replace("-", "").Replace(" ", "").Replace("+", "").Replace("(", "").Replace(")", "").Contains(digits)
                                                                || (g.Cnic != null && g.Cnic.Contains(digits)))));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(g => g.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(g => new GuardianListItemDto(g.Id, g.FullName, g.Cnic, g.Phone, g.Students.Count(x => !x.Student.IsDeleted)))
            .ToListAsync(ct);
        return new PagedResult<GuardianListItemDto>(items, total);
    }

    /// <summary>All of a guardian's children (filters ignored); the service hides ids of other campuses.</summary>
    public async Task<List<(int CampusId, SiblingDto Child)>> GetChildrenAsync(int guardianId, CancellationToken ct = default)
    {
        var rows = await Db.Students.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.Guardians.Any(g => g.GuardianId == guardianId))
            .OrderBy(s => s.FirstName)
            .Select(s => new
            {
                s.Id, s.CampusId, Name = s.FirstName + " " + s.LastName, CampusCode = s.Campus.Code,
                Current = s.Enrollments.Where(e => !e.IsDeleted && e.Status == EnrollmentStatus.Active)
                    .OrderByDescending(e => e.StartDate)
                    .Select(e => new { ClassName = e.Section.Class.Name, SectionName = e.Section.Name })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
        return rows.Select(r => (r.CampusId, new SiblingDto(r.Id, r.Name, r.CampusCode, r.Current?.ClassName, r.Current?.SectionName))).ToList();
    }

    /// <summary>Exact CNIC or phone match across the school; at most 5 rows, minimal fields only.</summary>
    public Task<List<GuardianLookupDto>> LookupAsync(string cnicDigits, string phoneDigits, CancellationToken ct = default) =>
        Db.Guardians.AsNoTracking()
            .Where(g => (cnicDigits != "" && g.Cnic == cnicDigits)
                        || (phoneDigits != "" && g.Phone.Replace("-", "").Replace(" ", "").Replace("+", "").Replace("(", "").Replace(")", "") == phoneDigits))
            .OrderBy(g => g.FullName)
            .Take(5)
            .Select(g => new GuardianLookupDto(g.Id, g.FullName, g.Students.Count(x => !x.Student.IsDeleted)))
            .ToListAsync(ct);

    /// <summary>True when the guardian is linked to a student visible to the request (campus filter applies).</summary>
    public Task<bool> IsVisibleAsync(int guardianId, CancellationToken ct = default) =>
        Db.Students.AnyAsync(s => s.Guardians.Any(g => g.GuardianId == guardianId), ct);
}
