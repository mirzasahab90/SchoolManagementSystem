using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Hiring;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class VacancyRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>Vacancies of the request's campuses with stage counts computed in SQL.</summary>
    public async Task<List<VacancyDto>> ListAsync(VacancyStatus? status, int? id = null, CancellationToken ct = default)
    {
        var query = Db.Vacancies.AsNoTracking().AsQueryable();
        if (status != null) query = query.Where(v => v.Status == status);
        if (id != null) query = query.Where(v => v.Id == id);

        var rows = await query
            .OrderBy(v => v.Status == VacancyStatus.Open ? 0 : v.Status == VacancyStatus.OnHold ? 1 : 2)
            .ThenByDescending(v => v.CreatedOn)
            .Select(v => new
            {
                v.Id, v.CampusId, CampusCode = v.Campus.Code, v.Title, v.Designation, v.Category, v.Positions, v.ClassIds,
                v.SubjectIds, v.Requirements, v.ClosingDate, v.Status,
                Applied = v.Candidates.Count(c => c.Stage == CandidateStage.Applied),
                Shortlisted = v.Candidates.Count(c => c.Stage == CandidateStage.Shortlisted),
                Interview = v.Candidates.Count(c => c.Stage == CandidateStage.Interview),
                Offered = v.Candidates.Count(c => c.Stage == CandidateStage.Offered),
                Hired = v.Candidates.Count(c => c.Stage == CandidateStage.Hired)
            })
            .ToListAsync(ct);

        return rows.Select(v => new VacancyDto(
            v.Id, v.CampusId, v.CampusCode, v.Title, v.Designation, v.Category, v.Positions, v.Hired,
            ParseIds(v.ClassIds), ParseIds(v.SubjectIds), v.Requirements, v.ClosingDate, v.Status,
            new StageCountsDto(v.Applied, v.Shortlisted, v.Interview, v.Offered, v.Hired))).ToList();
    }

    public Task<Vacancy?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Vacancies.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<int> CountHiredAsync(int vacancyId, CancellationToken ct = default) =>
        Db.Candidates.CountAsync(c => c.VacancyId == vacancyId && c.Stage == CandidateStage.Hired, ct);

    public Task<bool> HasCandidatesAsync(int vacancyId, CancellationToken ct = default) =>
        Db.Candidates.AnyAsync(c => c.VacancyId == vacancyId, ct);

    public Task<int> CountOpenAsync(CancellationToken ct = default) =>
        Db.Vacancies.CountAsync(v => v.Status == VacancyStatus.Open, ct);

    public Task<int> CountInterviewsBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default) =>
        Db.InterviewRounds.CountAsync(r => r.Result == InterviewResult.Scheduled && r.ScheduledAt >= from && r.ScheduledAt < to
                                           && !r.Candidate.IsDeleted, ct);

    /// <summary>True when every id is a class visible to the request.</summary>
    public async Task<bool> ClassesVisibleAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        ids.Count == 0 || await Db.Classes.CountAsync(c => ids.Contains(c.Id), ct) == ids.Count;

    public async Task<bool> SubjectsVisibleAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        ids.Count == 0 || await Db.Subjects.CountAsync(s => ids.Contains(s.Id), ct) == ids.Count;

    public static string? JoinIds(IEnumerable<int>? ids)
    {
        var list = ids?.Distinct().OrderBy(i => i).ToList();
        return list == null || list.Count == 0 ? null : string.Join(',', list);
    }

    public static IReadOnlyList<int> ParseIds(string? csv) =>
        string.IsNullOrEmpty(csv) ? [] : csv.Split(',').Select(int.Parse).ToList();
}

public class CandidateRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public async Task<PagedResult<CandidateListItemDto>> SearchAsync(
        int? vacancyId, string? stage, string? q, CandidateSource? source, DateOnly? appliedFrom, DateOnly? appliedTo,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = Db.Candidates.AsNoTracking().AsQueryable();
        if (vacancyId != null) query = query.Where(c => c.VacancyId == vacancyId);
        if (source != null) query = query.Where(c => c.Source == source);
        if (appliedFrom is DateOnly from)
        {
            var start = from.ToDateTime(TimeOnly.MinValue);
            query = query.Where(c => c.CreatedOn >= start);
        }

        if (appliedTo is DateOnly to)
        {
            var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(c => c.CreatedOn < end);
        }

        if (string.Equals(stage, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.Stage != CandidateStage.Rejected && c.Stage != CandidateStage.Withdrawn && c.Stage != CandidateStage.Hired);
        }
        else if (string.Equals(stage, "closed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.Stage == CandidateStage.Rejected || c.Stage == CandidateStage.Withdrawn);
        }
        else if (Enum.TryParse<CandidateStage>(stage, true, out var parsed))
        {
            query = query.Where(c => c.Stage == parsed);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            var digits = new string(text.Where(char.IsDigit).ToArray());
            query = query.Where(c =>
                c.FullName.Contains(text)
                || (c.Email != null && c.Email.Contains(text))
                || (digits.Length >= 4 && c.Cnic != null && c.Cnic.Contains(digits))
                || (digits.Length >= 4 && c.Phone.Replace("-", "").Replace(" ", "").Replace("+", "").Contains(digits)));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedOn)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CandidateListItemDto(
                c.Id, c.VacancyId, c.Vacancy.Title, c.FullName, c.Phone, c.Qualification, c.ExperienceYears, c.Stage,
                c.Interviews.Where(r => r.Rating != null).OrderByDescending(r => r.ScheduledAt).Select(r => r.Rating).FirstOrDefault(),
                c.CvPath != null, c.Campus.Code))
            .ToListAsync(ct);
        return new PagedResult<CandidateListItemDto>(items, total);
    }

    public Task<Candidate?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Candidates
            .Include(c => c.Vacancy)
            .Include(c => c.Campus)
            .Include(c => c.Interviews)
            .Include(c => c.Offers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> CnicTakenInVacancyAsync(int vacancyId, string cnic, int? excludeId, CancellationToken ct = default) =>
        Db.Candidates.IgnoreQueryFilters().AnyAsync(c =>
            !c.IsDeleted && c.VacancyId == vacancyId && c.Cnic == cnic && (excludeId == null || c.Id != excludeId), ct);

    /// <summary>Employee number of an active staff member with this CNIC on any campus, other than the one hired from this candidate.</summary>
    public Task<string?> ActiveStaffWithCnicAsync(string cnic, int? excludeCandidateId, CancellationToken ct = default) =>
        Db.Staff.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.Cnic == cnic && s.Status != StaffStatus.Resigned && s.Status != StaffStatus.Terminated
                        && (excludeCandidateId == null || s.SourceCandidateId != excludeCandidateId))
            .Select(s => s.EmployeeNo)
            .FirstOrDefaultAsync(ct);

    public Task<List<StageHistoryDto>> GetHistoryAsync(int candidateId, CancellationToken ct = default) =>
        Db.CandidateStageHistory.AsNoTracking()
            .Where(h => h.CandidateId == candidateId)
            .OrderBy(h => h.OccurredOn).ThenBy(h => h.Id)
            .Select(h => new StageHistoryDto(h.FromStage, h.ToStage, h.Reason, h.OccurredOn,
                Db.Users.IgnoreQueryFilters().Where(u => u.Id == h.ByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<List<InterviewRoundDto>> GetInterviewsAsync(int candidateId, CancellationToken ct = default)
    {
        var rounds = await Db.InterviewRounds.AsNoTracking()
            .Where(r => r.CandidateId == candidateId)
            .OrderBy(r => r.ScheduledAt).ThenBy(r => r.Id)
            .ToListAsync(ct);

        // Separate query: IgnoreQueryFilters inside the rounds query would also un-filter deleted rounds.
        // Ignoring the user filter keeps a deleted interviewer's rounds visible with their name.
        var ids = rounds.Select(r => r.InterviewerUserId).Distinct().ToList();
        var names = await Db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => (u.FirstName + " " + u.LastName).Trim(), ct);

        return rounds.Select(r => new InterviewRoundDto(r.Id, r.Name, r.ScheduledAt, r.InterviewerUserId,
            names.GetValueOrDefault(r.InterviewerUserId, "Unknown"), r.Result, r.Rating, r.Notes)).ToList();
    }

    public void AddHistory(CandidateStageHistory history) => Db.CandidateStageHistory.Add(history);
}

public class StaffRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public async Task<PagedResult<StaffListItemDto>> SearchAsync(
        string? q, StaffStatus? status, StaffCategory? category, StaffDesignation? designation, string? sort, bool descending,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = Db.Staff.AsNoTracking().AsQueryable();
        if (status != null) query = query.Where(s => s.Status == status);
        if (category != null) query = query.Where(s => s.Category == category);
        if (designation != null) query = query.Where(s => s.Designation == designation);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();
            var digits = new string(text.Where(char.IsDigit).ToArray());
            query = query.Where(s =>
                (s.FirstName + " " + s.LastName).Contains(text)
                || s.EmployeeNo.Contains(text)
                || (digits.Length >= 4 && s.Cnic != null && s.Cnic.Contains(digits))
                || (digits.Length >= 4 && s.Phone.Replace("-", "").Replace(" ", "").Replace("+", "").Contains(digits)));
        }

        var desc = descending;
        var ordered = (sort?.Trim().ToLowerInvariant()) switch
        {
            "name" => query.OrderByDir(s => s.FirstName, desc).ThenByDir(s => s.LastName, desc).ThenByDir(s => s.Id, desc),
            "employeeno" => query.OrderByDir(s => s.EmployeeNo, desc).ThenByDir(s => s.Id, desc),
            // Stored as text, so this is alphabetical by designation name.
            "designation" => query.OrderByDir(s => s.Designation, desc).ThenBy(s => s.FirstName).ThenBy(s => s.LastName).ThenBy(s => s.Id),
            "joined" => query.OrderByDir(s => s.JoiningDate, desc).ThenByDir(s => s.Id, desc),
            // Unknown or missing sort: the original order (dir is ignored too).
            _ => query.OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
        };

        var total = await query.CountAsync(ct);
        var items = await ordered
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new StaffListItemDto(s.Id, s.EmployeeNo, (s.FirstName + " " + s.LastName).Trim(), s.Designation, s.Category,
                s.JoiningDate, s.Status, s.Campus.Code))
            .ToListAsync(ct);
        return new PagedResult<StaffListItemDto>(items, total);
    }

    /// <summary>Distinct designations of the request's campuses' staff, alphabetical.</summary>
    public async Task<List<StaffDesignation>> DesignationsAsync(CancellationToken ct = default)
    {
        var used = await Db.Staff.AsNoTracking().Select(s => s.Designation).Distinct().ToListAsync(ct);
        return used.OrderBy(d => d.ToString(), StringComparer.Ordinal).ToList();
    }

    public Task<StaffDetailDto?> GetDetailAsync(int id, CancellationToken ct = default) =>
        Db.Staff.AsNoTracking().Where(s => s.Id == id).Select(s => new StaffDetailDto(
            s.Id, s.CampusId, s.Campus.Code, s.EmployeeNo, s.FirstName, s.LastName, (s.FirstName + " " + s.LastName).Trim(),
            s.Cnic, s.Phone, s.Email, s.Category, s.Designation, s.EmploymentType, s.JoiningDate, s.BasicSalary, s.Status,
            s.SourceCandidateId)).FirstOrDefaultAsync(ct);

    /// <summary>True when a non-deleted, not resigned / terminated staff member on any campus has this CNIC.</summary>
    public Task<bool> CnicActiveAsync(string cnic, CancellationToken ct = default, int? excludeId = null) =>
        Db.Staff.IgnoreQueryFilters().AnyAsync(s =>
            !s.IsDeleted && s.Cnic == cnic && s.Status != StaffStatus.Resigned && s.Status != StaffStatus.Terminated
            && (excludeId == null || s.Id != excludeId), ct);

    /// <summary>The staff member with documents and assignments (campus-filtered).</summary>
    public Task<Staff?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Staff.Include(s => s.Campus).Include(s => s.Documents).Include(s => s.Assignments)
            .AsSplitQuery().FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <summary>Open assignments of the given (current) session; none when there is no current session.</summary>
    public Task<List<AssignmentDto>> GetCurrentAssignmentsAsync(int staffId, int? sessionId, CancellationToken ct = default) =>
        Db.TeachingAssignments.AsNoTracking()
            .Where(a => a.StaffId == staffId && a.EndedOn == null && a.AcademicSessionId == sessionId)
            .OrderBy(a => a.Section.Class.DisplayOrder).ThenBy(a => a.Section.Name).ThenBy(a => a.Subject.Name)
            .Select(a => new AssignmentDto(a.Id, a.SectionId, a.Section.Class.Name, a.Section.Name, a.SubjectId, a.Subject.Name, a.StartedOn))
            .ToListAsync(ct);

    public Task<List<ClassTeacherDto>> GetClassTeacherOfAsync(int staffId, CancellationToken ct = default) =>
        Db.Sections.AsNoTracking()
            .Where(s => s.ClassTeacherStaffId == staffId)
            .OrderBy(s => s.Class.DisplayOrder).ThenBy(s => s.Name)
            .Select(s => new ClassTeacherDto(s.Id, s.Class.Name, s.Name))
            .ToListAsync(ct);

    public async Task<StaffAccountDto?> GetAccountAsync(int userId, CancellationToken ct = default)
    {
        var user = await Db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Id, u.Email, u.IsActive, u.MustChangePassword, Roles = u.UserRoles.Select(r => r.Role.Name).ToList() })
            .FirstOrDefaultAsync(ct);
        return user == null ? null : new StaffAccountDto(user.Id, user.Email, user.Roles, user.IsActive, user.MustChangePassword);
    }

    /// <summary>
    /// True when another non-deleted staff member is linked to this user, other than a left (Resigned / Terminated)
    /// record of the given campus, which gives the login up on relink.
    /// </summary>
    public Task<bool> UserLinkedAsync(int userId, int exceptStaffId, CancellationToken ct = default) =>
        Db.Staff.IgnoreQueryFilters().AnyAsync(s => !s.IsDeleted && s.UserId == userId && s.Id != exceptStaffId
            && s.Status != StaffStatus.Resigned && s.Status != StaffStatus.Terminated, ct);

    /// <summary>Left records of this campus still holding the login (tracked, campus-filtered).</summary>
    public Task<List<Staff>> LeftRecordsLinkedToAsync(int userId, int campusId, CancellationToken ct = default) =>
        Db.Staff.Where(s => s.UserId == userId && s.CampusId == campusId
            && (s.Status == StaffStatus.Resigned || s.Status == StaffStatus.Terminated)).ToListAsync(ct);

    /// <summary>Teaching staff of a campus who have not left, for the class-teacher picker.</summary>
    public Task<List<ClassTeacherOptionDto>> TeacherOptionsAsync(int campusId, CancellationToken ct = default) =>
        Db.Staff.AsNoTracking()
            .Where(s => s.CampusId == campusId && s.Category == StaffCategory.Teaching
                        && s.Status != StaffStatus.Resigned && s.Status != StaffStatus.Terminated)
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new ClassTeacherOptionDto(s.Id, (s.FirstName + " " + s.LastName).Trim(), s.EmployeeNo))
            .ToListAsync(ct);

    public Task<bool> HasAnyAssignmentAsync(int staffId, CancellationToken ct = default) =>
        Db.TeachingAssignments.IgnoreQueryFilters().AnyAsync(a => a.StaffId == staffId && !a.IsDeleted, ct);

    public Task<bool> EmployeeNoTakenAsync(int campusId, string employeeNo, CancellationToken ct = default) =>
        Db.Staff.IgnoreQueryFilters().AnyAsync(s => !s.IsDeleted && s.CampusId == campusId && s.EmployeeNo == employeeNo, ct);
}
