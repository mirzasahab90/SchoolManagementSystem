using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Exams;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

/// <summary>Grade scales and result schemes (campus-owned rows; null = Main Campus).</summary>
public class GradingRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>The campus's scale, else the Main Campus scale (ignores the request filter; callers pass a visible campus).</summary>
    public async Task<GradeScale?> ResolveScaleAsync(int? campusId, CancellationToken ct = default)
    {
        var scales = await Db.GradeScales.IgnoreQueryFilters().AsNoTracking().Include(s => s.Bands)
            .Where(s => !s.IsDeleted && (s.CampusId == null || s.CampusId == campusId)).ToListAsync(ct);
        return scales.FirstOrDefault(s => s.CampusId != null) ?? scales.FirstOrDefault(s => s.CampusId == null);
    }

    public Task<GradeScale?> GetOwnedTrackedAsync(int? campusId, CancellationToken ct = default) =>
        Db.GradeScales.Include(s => s.Bands).FirstOrDefaultAsync(s => s.CampusId == campusId, ct);

    public Task<List<ResultScheme>> SchemesForAsync(int? campusId, CancellationToken ct = default) =>
        Db.ResultSchemes.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && (s.CampusId == null || s.CampusId == campusId)).ToListAsync(ct);

    public Task<ResultScheme?> GetSchemeTrackedAsync(int id, CancellationToken ct = default) =>
        Db.ResultSchemes.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<ResultScheme?> FindSchemeTrackedAsync(int? campusId, int? sessionId, int? classId, CancellationToken ct = default) =>
        Db.ResultSchemes.FirstOrDefaultAsync(s => s.CampusId == campusId && s.AcademicSessionId == sessionId && s.ClassId == classId, ct);

    public void RemoveBands(IEnumerable<GradeBand> bands) => Db.GradeBands.RemoveRange(bands);
}

/// <summary>Exams, papers, paper sections and publish state (filtered by campus / ownership).</summary>
public class ExamRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<List<Exam>> ListAsync(int? sessionId, CancellationToken ct = default) =>
        Db.Exams.AsNoTracking().Include(e => e.Campus).Include(e => e.AcademicSession)
            .Where(e => sessionId == null || e.AcademicSessionId == sessionId)
            .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.Id)
            .ToListAsync(ct);

    public Task<Exam?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.Exams.Include(e => e.Campus).Include(e => e.AcademicSession)
            .Include(e => e.Papers).ThenInclude(p => p.Subject)
            .Include(e => e.Papers).ThenInclude(p => p.Class)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> NameTakenAsync(int? campusId, int sessionId, string name, int? excludeId, CancellationToken ct = default) =>
        Db.Exams.IgnoreQueryFilters().AnyAsync(e => !e.IsDeleted && e.CampusId == campusId && e.AcademicSessionId == sessionId
                                                    && e.Name == name && (excludeId == null || e.Id != excludeId), ct);

    public Task<bool> HasMarksAsync(int examId, CancellationToken ct = default) =>
        Db.Marks.IgnoreQueryFilters().AnyAsync(m => !m.IsDeleted && m.ExamPaper.ExamId == examId, ct);

    /// <summary>Highest entered theory and practical marks in a paper (any campus).</summary>
    public async Task<(decimal? Theory, decimal? Practical)> MaxMarksAsync(int paperId, CancellationToken ct = default)
    {
        var rows = Db.Marks.IgnoreQueryFilters().Where(m => !m.IsDeleted && m.ExamPaperId == paperId);
        return (await rows.MaxAsync(m => m.Theory, ct), await rows.MaxAsync(m => m.Practical, ct));
    }

    /// <summary>Visible sections of the exam's session in the given classes.</summary>
    /// <summary>Active sections of the classes in a session; a campus-owned exam's sections are only that campus's.</summary>
    public Task<List<Section>> SectionsAsync(int sessionId, IReadOnlyCollection<int> classIds, int? examCampusId, CancellationToken ct = default) =>
        Db.Sections.AsNoTracking().Include(s => s.Class).Include(s => s.Campus)
            .Where(s => s.AcademicSessionId == sessionId && classIds.Contains(s.ClassId) && s.IsActive
                        && (examCampusId == null || s.CampusId == examCampusId))
            .OrderBy(s => s.Class.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);

    public Task<List<PaperSection>> PaperSectionsAsync(IReadOnlyCollection<int> paperIds, CancellationToken ct = default) =>
        Db.PaperSections.AsNoTracking().Where(p => paperIds.Contains(p.ExamPaperId)).ToListAsync(ct);

    public Task<List<SectionResult>> SectionResultsAsync(int examId, CancellationToken ct = default) =>
        Db.SectionResults.AsNoTracking().Where(r => r.ExamId == examId).ToListAsync(ct);

    public Task<List<ClassSubject>> ClassSubjectsAsync(IReadOnlyCollection<int> classIds, CancellationToken ct = default) =>
        Db.ClassSubjects.AsNoTracking().Where(c => classIds.Contains(c.ClassId)).ToListAsync(ct);

    public Task<int> VisibleClassCountAsync(IReadOnlyCollection<int> classIds, CancellationToken ct = default) =>
        Db.Classes.CountAsync(c => classIds.Contains(c.Id), ct);
}

/// <summary>Marks, paper-section progress and exam rosters.</summary>
public class MarkRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public record RosterRow(int StudentId, int RollNo, string Name);

    public Task<ExamPaper?> GetPaperAsync(int paperId, CancellationToken ct = default) =>
        Db.ExamPapers.Include(p => p.Exam).ThenInclude(e => e.AcademicSession).Include(p => p.Subject).Include(p => p.Class)
            .FirstOrDefaultAsync(p => p.Id == paperId, ct);

    /// <summary>
    /// Students on a paper's sheet for a section: enrolled in it on the date (minus anyone whose marks for this paper sit with
    /// another section), plus anyone whose marks for this paper sit with this section.
    /// </summary>
    public async Task<List<RosterRow>> RosterAsync(int paperId, int sectionId, DateOnly date, CancellationToken ct = default)
    {
        var enrolled = await Db.Enrollments.AsNoTracking()
            .Where(e => e.SectionId == sectionId && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date) && !e.Student.IsDeleted
                        && !Db.Marks.Any(m => m.ExamPaperId == paperId && m.StudentId == e.StudentId && m.SectionId != sectionId && !m.IsDeleted))
            .Select(e => new RosterRow(e.StudentId, e.RollNo, (e.Student.FirstName + " " + e.Student.LastName).Trim()))
            .ToListAsync(ct);
        var here = await Db.Marks.AsNoTracking()
            .Where(m => m.ExamPaperId == paperId && m.SectionId == sectionId && !m.Student.IsDeleted)
            .Select(m => new RosterRow(m.StudentId,
                Db.Enrollments.Where(e => e.StudentId == m.StudentId && e.EndDate == null).Select(e => e.RollNo).FirstOrDefault(),
                (m.Student.FirstName + " " + m.Student.LastName).Trim()))
            .ToListAsync(ct);
        return enrolled.Concat(here.Where(h => enrolled.All(e => e.StudentId != h.StudentId)))
            .OrderBy(r => r.RollNo).ThenBy(r => r.Name).ToList();
    }

    public Task<List<Mark>> MarksTrackedAsync(int paperId, int sectionId, CancellationToken ct = default) =>
        Db.Marks.Where(m => m.ExamPaperId == paperId && m.SectionId == sectionId).ToListAsync(ct);

    public Task<PaperSection?> PaperSectionTrackedAsync(int paperId, int sectionId, CancellationToken ct = default) =>
        Db.PaperSections.FirstOrDefaultAsync(p => p.ExamPaperId == paperId && p.SectionId == sectionId, ct);

    public Task<bool> IsPublishedAsync(int examId, int sectionId, CancellationToken ct = default) =>
        Db.SectionResults.AnyAsync(r => r.ExamId == examId && r.SectionId == sectionId && r.IsPublished, ct);

    /// <summary>Current assignments of a staff member with their section (for "my papers").</summary>
    public Task<List<(int SectionId, int SubjectId, int ClassId, int SessionId)>> AssignmentsAsync(int staffId, CancellationToken ct = default) =>
        Db.TeachingAssignments.AsNoTracking().Where(a => a.StaffId == staffId && a.EndedOn == null)
            .Select(a => new ValueTuple<int, int, int, int>(a.SectionId, a.SubjectId, a.Section.ClassId, a.Section.AcademicSessionId))
            .ToListAsync(ct);

    public Task<List<ExamPaper>> PapersForAsync(int? examId, CancellationToken ct = default) =>
        Db.ExamPapers.AsNoTracking().Include(p => p.Exam).Include(p => p.Subject).Include(p => p.Class)
            .Where(p => p.IsActive && (examId == null || p.ExamId == examId) && p.Exam.Status != ExamStatus.Draft)
            .ToListAsync(ct);

    public Task<List<Section>> SectionsByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        Db.Sections.AsNoTracking().Include(s => s.Class).Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public Task<List<PaperSection>> PaperSectionsForAsync(IReadOnlyCollection<int> paperIds, CancellationToken ct = default) =>
        Db.PaperSections.AsNoTracking().Where(p => paperIds.Contains(p.ExamPaperId)).ToListAsync(ct);

    public Task<List<SectionResult>> PublishedForAsync(IReadOnlyCollection<int> examIds, CancellationToken ct = default) =>
        Db.SectionResults.AsNoTracking().Where(r => examIds.Contains(r.ExamId) && r.IsPublished).ToListAsync(ct);
}

/// <summary>Publish state, result snapshots and remarks.</summary>
public class ResultRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public record StudentName(int StudentId, int RollNo, string Name);

    public Task<Exam?> GetExamAsync(int examId, CancellationToken ct = default) =>
        Db.Exams.AsNoTracking().Include(e => e.AcademicSession).Include(e => e.Papers.Where(p => p.IsActive)).ThenInclude(p => p.Subject)
            .FirstOrDefaultAsync(e => e.Id == examId, ct);

    public Task<SectionResult?> SectionResultTrackedAsync(int examId, int sectionId, CancellationToken ct = default) =>
        Db.SectionResults.FirstOrDefaultAsync(r => r.ExamId == examId && r.SectionId == sectionId, ct);

    /// <summary>The students' marks for these papers in whichever section they were entered.</summary>
    public Task<List<Mark>> MarksForStudentsAsync(IReadOnlyCollection<int> paperIds, IReadOnlyCollection<int> studentIds, CancellationToken ct = default) =>
        Db.Marks.AsNoTracking().Where(m => paperIds.Contains(m.ExamPaperId) && studentIds.Contains(m.StudentId)).ToListAsync(ct);

    /// <summary>Students enrolled in the section on the date.</summary>
    public Task<List<MarkRepository.RosterRow>> EnrolledOnAsync(int sectionId, DateOnly date, CancellationToken ct = default) =>
        Db.Enrollments.AsNoTracking()
            .Where(e => e.SectionId == sectionId && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date) && !e.Student.IsDeleted)
            .Select(e => new MarkRepository.RosterRow(e.StudentId, e.RollNo, (e.Student.FirstName + " " + e.Student.LastName).Trim()))
            .ToListAsync(ct);

    /// <summary>For each student, the section of this class and session they are enrolled in on the date (if any).</summary>
    public async Task<Dictionary<int, int>> EnrolledSectionOnAsync(
        IReadOnlyCollection<int> studentIds, int classId, int sessionId, DateOnly date, CancellationToken ct = default)
    {
        var rows = await Db.Enrollments.AsNoTracking()
            .Where(e => studentIds.Contains(e.StudentId) && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date)
                        && e.Section.ClassId == classId && e.Section.AcademicSessionId == sessionId)
            .Select(e => new { e.StudentId, e.SectionId }).ToListAsync(ct);
        return rows.GroupBy(r => r.StudentId).ToDictionary(g => g.Key, g => g.First().SectionId);
    }

    /// <summary>Students who already have a published result for this exam in another section.</summary>
    public async Task<HashSet<int>> PublishedElsewhereAsync(int examId, int sectionId, IReadOnlyCollection<int> studentIds, CancellationToken ct = default) =>
        (await Db.StudentResults.AsNoTracking().Where(r => r.ExamId == examId && r.SectionId != sectionId && studentIds.Contains(r.StudentId))
            .Select(r => r.StudentId).ToListAsync(ct)).ToHashSet();

    public Task<List<StudentResult>> SnapshotsTrackedAsync(int examId, int sectionId, CancellationToken ct = default) =>
        Db.StudentResults.Where(r => r.ExamId == examId && r.SectionId == sectionId).ToListAsync(ct);

    /// <summary>Name and roll number (in this section, latest enrolment) for snapshot rows.</summary>
    public async Task<List<StudentName>> NamesAsync(IReadOnlyCollection<int> studentIds, int sectionId, CancellationToken ct = default)
    {
        var students = await Db.Students.AsNoTracking().IgnoreQueryFilters().Where(s => studentIds.Contains(s.Id))
            .Select(s => new { s.Id, Name = (s.FirstName + " " + s.LastName).Trim() }).ToListAsync(ct);
        var rolls = await Db.Enrollments.AsNoTracking().Where(e => studentIds.Contains(e.StudentId) && e.SectionId == sectionId)
            .OrderByDescending(e => e.StartDate).Select(e => new { e.StudentId, e.RollNo }).ToListAsync(ct);
        return students.Select(s => new StudentName(s.Id, rolls.FirstOrDefault(r => r.StudentId == s.Id)?.RollNo ?? 0, s.Name)).ToList();
    }

    /// <summary>The section the student is currently enrolled in for a session (null when none).</summary>
    public Task<Section?> CurrentSectionAsync(int studentId, int sessionId, CancellationToken ct = default) =>
        Db.Enrollments.AsNoTracking().Where(e => e.StudentId == studentId && e.EndDate == null && e.Section.AcademicSessionId == sessionId)
            .Select(e => e.Section).FirstOrDefaultAsync(ct);

    public Task<ResultRemark?> RemarkTrackedAsync(int? examId, int sessionId, int studentId, CancellationToken ct = default) =>
        Db.ResultRemarks.FirstOrDefaultAsync(r => r.ExamId == examId && r.AcademicSessionId == sessionId && r.StudentId == studentId, ct);

    public record PublishedRow(StudentResult Result, int ExamId, string ExamName, ExamType Type, DateOnly? StartDate);

    /// <summary>Published snapshots of these students in the session's exams.</summary>
    public Task<List<PublishedRow>> PublishedForStudentsAsync(IReadOnlyCollection<int> studentIds, int sessionId, CancellationToken ct = default) =>
        Db.StudentResults.AsNoTracking().Where(r => studentIds.Contains(r.StudentId))
            .Join(Db.Exams.Where(e => e.AcademicSessionId == sessionId), r => r.ExamId, e => e.Id,
                (r, e) => new PublishedRow(r, e.Id, e.Name, e.Type, e.StartDate))
            .ToListAsync(ct);

    /// <summary>Students currently in the section plus anyone with a published result in it this session.</summary>
    public async Task<List<StudentName>> SectionStudentsAsync(int sectionId, int sessionId, CancellationToken ct = default)
    {
        var current = await Db.Enrollments.AsNoTracking().Where(e => e.SectionId == sectionId && e.EndDate == null && !e.Student.IsDeleted)
            .Select(e => e.StudentId).ToListAsync(ct);
        var examIds = Db.Exams.Where(e => e.AcademicSessionId == sessionId).Select(e => e.Id);
        var published = await Db.StudentResults.AsNoTracking().Where(r => r.SectionId == sectionId && examIds.Contains(r.ExamId))
            .Select(r => r.StudentId).Distinct().ToListAsync(ct);
        var names = await NamesAsync(current.Union(published).ToList(), sectionId, ct);
        return names.OrderBy(n => n.RollNo).ThenBy(n => n.Name).ToList();
    }

    public Task<List<SchoolManagementSystem.Domain.Entities.Students.Student>> StudentsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        Db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public Task<List<ResultRemark>> RemarksAsync(int? examId, int sessionId, IReadOnlyCollection<int> studentIds, CancellationToken ct = default) =>
        Db.ResultRemarks.AsNoTracking().Where(r => r.ExamId == examId && r.AcademicSessionId == sessionId && studentIds.Contains(r.StudentId))
            .ToListAsync(ct);

    public Task<StudentResult?> SnapshotAsync(int examId, int studentId, CancellationToken ct = default) =>
        Db.StudentResults.AsNoTracking().FirstOrDefaultAsync(r => r.ExamId == examId && r.StudentId == studentId, ct);

    /// <summary>The latest open exam visible here with (submitted, total) paper-section counts.</summary>
    public async Task<(string? Name, int Submitted, int Total)> OpenExamProgressAsync(CancellationToken ct = default)
    {
        var exam = await Db.Exams.AsNoTracking().Where(e => e.Status == ExamStatus.Open)
            .OrderByDescending(e => e.StartDate).ThenByDescending(e => e.Id).FirstOrDefaultAsync(ct);
        if (exam == null) return (null, 0, 0);
        var papers = await Db.ExamPapers.AsNoTracking().Where(p => p.ExamId == exam.Id && p.IsActive).Select(p => new { p.Id, p.ClassId }).ToListAsync(ct);
        var classIds = papers.Select(p => p.ClassId).Distinct().ToList();
        var sections = await Db.Sections.AsNoTracking()
            .Where(s => s.AcademicSessionId == exam.AcademicSessionId && classIds.Contains(s.ClassId) && s.IsActive
                        && (exam.CampusId == null || s.CampusId == exam.CampusId))
            .Select(s => new { s.Id, s.ClassId }).ToListAsync(ct);
        var total = papers.Sum(p => sections.Count(s => s.ClassId == p.ClassId));
        var paperIds = papers.Select(p => p.Id).ToList();
        var sectionIds = sections.Select(s => s.Id).ToList();
        var submitted = await Db.PaperSections.CountAsync(x => paperIds.Contains(x.ExamPaperId) && sectionIds.Contains(x.SectionId)
                                                               && x.Status == PaperSectionStatus.Submitted, ct);
        return (exam.Name, submitted, total);
    }
}
