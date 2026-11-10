using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Application.Services.Academics;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Exams;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Application.Services.Exams;

/// <summary>Exams of a session and their papers. Main Campus exams (CampusId null) are sat by every campus.</summary>
public class ExamService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IExamService
{
    public async Task<Result<IReadOnlyList<ExamDto>>> ListAsync(int? sessionId, CancellationToken ct = default)
    {
        var exams = await repositories.Exams.ListAsync(sessionId, ct);
        var result = new List<ExamDto>();
        foreach (var e in exams)
        {
            var tracked = await repositories.Exams.GetTrackedAsync(e.Id, ct);
            result.Add((await BuildDetailAsync(tracked!, ct)).Exam);
        }

        return result;
    }

    public async Task<Result<ExamDetailDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var exam = await repositories.Exams.GetTrackedAsync(id, ct);
        return exam == null ? Error.NotFound("Exam") : await BuildDetailAsync(exam, ct);
    }

    public async Task<Result<ExamDetailDto>> CreateAsync(SaveExamRequest request, CancellationToken ct = default)
    {
        int? owner;
        if (request.MainCampus)
        {
            if (!currentUser.IsHq) return Ownership.HqItem;
            owner = null;
        }
        else
        {
            if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
            owner = campusId;
        }

        var session = await repositories.Sessions.GetTrackedAsync(request.SessionId, ct);
        if (session == null) return Error.Validation("Pick a session.");
        var error = Validate(request, session);
        if (error != null) return error;
        if (await repositories.Exams.NameTakenAsync(owner, session.Id, request.Name.Trim(), null, ct))
            return Error.Conflict("An exam with this name already exists in the session.");

        var classIds = (request.ClassIds ?? []).Distinct().ToList();
        if (classIds.Count == 0) return Error.Validation("Pick at least one class.");
        if (await repositories.Exams.VisibleClassCountAsync(classIds, ct) != classIds.Count) return Error.Validation("Pick classes of this campus.");

        var exam = new Exam { CampusId = owner, AcademicSessionId = session.Id, Status = ExamStatus.Draft };
        Apply(exam, request);
        await AddPapersAsync(exam, classIds, ct);
        repositories.Exams.Add(exam);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(exam.Id, ct);
    }

    public async Task<Result<ExamDetailDto>> UpdateAsync(int id, SaveExamRequest request, CancellationToken ct = default)
    {
        var exam = await repositories.Exams.GetTrackedAsync(id, ct);
        if (exam == null) return Error.NotFound("Exam");
        var write = Ownership.CheckWrite(currentUser, exam.CampusId, "Exam");
        if (write != null) return write;
        var error = Validate(request, exam.AcademicSession);
        if (error != null) return error;
        if (await repositories.Exams.NameTakenAsync(exam.CampusId, exam.AcademicSessionId, request.Name.Trim(), id, ct))
            return Error.Conflict("An exam with this name already exists in the session.");
        if (request.Type != exam.Type && await repositories.Exams.HasMarksAsync(id, ct))
            return Error.Validation("The type cannot change once marks are entered.");

        Apply(exam, request);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result<ExamDto>> SetStatusAsync(int id, ExamStatus status, CancellationToken ct = default)
    {
        var exam = await repositories.Exams.GetTrackedAsync(id, ct);
        if (exam == null) return Error.NotFound("Exam");
        var write = Ownership.CheckWrite(currentUser, exam.CampusId, "Exam");
        if (write != null) return write;
        if (!Enum.IsDefined(status)) return Error.Validation("Unknown status.");
        if (status == ExamStatus.Draft && exam.Status != ExamStatus.Draft && await repositories.Exams.HasMarksAsync(id, ct))
            return Error.Validation("An exam with marks cannot go back to draft.");

        exam.Status = status;
        await repositories.SaveChangesAsync(ct);
        return (await BuildDetailAsync(exam, ct)).Exam;
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var exam = await repositories.Exams.GetTrackedAsync(id, ct);
        if (exam == null) return Error.NotFound("Exam");
        var write = Ownership.CheckWrite(currentUser, exam.CampusId, "Exam");
        if (write != null) return write;
        if (await repositories.Exams.HasMarksAsync(id, ct)) return Error.Validation("This exam has marks. Close it instead.");

        foreach (var p in exam.Papers) repositories.Exams.Remove(p);
        repositories.Exams.Remove(exam);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Exam deleted.");
    }

    public async Task<Result<PaperDto>> UpdatePaperAsync(int examId, int paperId, SavePaperRequest r, CancellationToken ct = default)
    {
        var exam = await repositories.Exams.GetTrackedAsync(examId, ct);
        var paper = exam?.Papers.FirstOrDefault(p => p.Id == paperId);
        if (exam == null || paper == null) return Error.NotFound("Paper");
        var write = Ownership.CheckWrite(currentUser, exam.CampusId, "Exam");
        if (write != null) return write;
        if (exam.Status == ExamStatus.Closed) return Error.Validation("This exam is closed.");

        if (r.TotalMarks is <= 0 or > 1000) return Error.Validation("Total marks must be between 1 and 1000.");
        if (r.PassMarks <= 0 || r.PassMarks > r.TotalMarks) return Error.Validation("Pass marks must be above 0 and not above the total.");
        if (r.HasPractical && (r.PracticalMarks is not decimal p || p <= 0 || p >= r.TotalMarks))
            return Error.Validation("Practical marks must be above 0 and below the total.");
        if (r.PaperDate is DateOnly d && (d < exam.AcademicSession.StartDate || d > exam.AcademicSession.EndDate))
            return Error.Validation("The paper date must be inside the session.");

        var theoryTotal = r.HasPractical ? r.TotalMarks - r.PracticalMarks!.Value : r.TotalMarks;
        var (maxTheory, maxPractical) = await repositories.Exams.MaxMarksAsync(paperId, ct);
        if (maxTheory > theoryTotal || (maxPractical > 0 && (!r.HasPractical || maxPractical > r.PracticalMarks)))
            return Error.Validation("Some entered marks are above the new total. Correct them first.");

        paper.TotalMarks = r.TotalMarks;
        paper.PassMarks = r.PassMarks;
        paper.HasPractical = r.HasPractical;
        paper.PracticalMarks = r.HasPractical ? r.PracticalMarks : null;
        paper.PaperDate = r.PaperDate;
        paper.IsActive = r.IsActive;
        await repositories.SaveChangesAsync(ct);
        return ToPaper(paper);
    }

    public async Task<Result<ExamDetailDto>> AddClassesAsync(int examId, IReadOnlyList<int> classIds, CancellationToken ct = default)
    {
        var exam = await repositories.Exams.GetTrackedAsync(examId, ct);
        if (exam == null) return Error.NotFound("Exam");
        var write = Ownership.CheckWrite(currentUser, exam.CampusId, "Exam");
        if (write != null) return write;
        var ids = (classIds ?? []).Distinct().Where(c => exam.Papers.All(p => p.ClassId != c)).ToList();
        if (await repositories.Exams.VisibleClassCountAsync(ids, ct) != ids.Count) return Error.Validation("Pick classes of this campus.");
        await AddPapersAsync(exam, ids, ct);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(examId, ct);
    }

    // ---------------------------------------------------------------- helpers

    private async Task AddPapersAsync(Exam exam, IReadOnlyCollection<int> classIds, CancellationToken ct)
    {
        var monthly = exam.Type == ExamType.Monthly;
        foreach (var cs in await repositories.Exams.ClassSubjectsAsync(classIds, ct))
        {
            exam.Papers.Add(new ExamPaper
            {
                CampusId = exam.CampusId,
                ClassId = cs.ClassId,
                SubjectId = cs.SubjectId,
                TotalMarks = monthly ? 25 : 100,
                PassMarks = monthly ? 9 : 33,
                IsActive = true
            });
        }
    }

    internal async Task<ExamDetailDto> BuildDetailAsync(Exam exam, CancellationToken ct)
    {
        var classIds = exam.Papers.Select(p => p.ClassId).Distinct().ToList();
        var sections = await repositories.Exams.SectionsAsync(exam.AcademicSessionId, classIds, exam.CampusId, ct);
        var paperSections = await repositories.Exams.PaperSectionsAsync(exam.Papers.Select(p => p.Id).ToList(), ct);
        var published = (await repositories.Exams.SectionResultsAsync(exam.Id, ct)).Where(r => r.IsPublished).Select(r => r.SectionId).ToHashSet();

        var classes = classIds.Select(cid =>
        {
            var papers = exam.Papers.Where(p => p.ClassId == cid).OrderBy(p => p.Subject.Name).ToList();
            var activeIds = papers.Where(p => p.IsActive).Select(p => p.Id).ToHashSet();
            var progress = sections.Where(s => s.ClassId == cid).Select(s => new SectionProgressDto(
                s.Id, s.Name,
                paperSections.Count(ps => ps.SectionId == s.Id && activeIds.Contains(ps.ExamPaperId) && ps.Status == PaperSectionStatus.Submitted),
                activeIds.Count, published.Contains(s.Id))).ToList();
            var name = papers.FirstOrDefault()?.Class.Name ?? sections.FirstOrDefault(s => s.ClassId == cid)?.Class.Name ?? "";
            return (Order: papers.FirstOrDefault()?.Class.DisplayOrder ?? 0, Dto: new ExamClassDto(cid, name, papers.Select(ToPaper).ToList(), progress));
        }).OrderBy(x => x.Order).Select(x => x.Dto).ToList();

        var all = classes.SelectMany(c => c.Sections).ToList();
        var dto = new ExamDto(exam.Id, exam.CampusId, exam.Campus?.Code, exam.AcademicSessionId, exam.AcademicSession.Name, exam.Name, exam.Type,
            exam.StartDate, exam.EndDate, exam.Status, all.Count, all.Count(s => s.Papers > 0 && s.Submitted == s.Papers), all.Count(s => s.Published));
        return new ExamDetailDto(dto, classes);
    }

    internal static PaperDto ToPaper(ExamPaper p) =>
        new(p.Id, p.SubjectId, p.Subject?.Name ?? "", p.TotalMarks, p.PassMarks, p.HasPractical, p.PracticalMarks, p.PaperDate, p.IsActive);

    private static Error? Validate(SaveExamRequest r, AcademicSession session)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100) return Error.Validation("Give the exam a name (100 characters at most).");
        if (!Enum.IsDefined(r.Type)) return Error.Validation("Pick an exam type.");
        if (session.Status == SessionStatus.Closed) return Error.Validation("This session is closed.");
        if (r.StartDate is DateOnly s && (s < session.StartDate || s > session.EndDate)) return Error.Validation("Exam dates must be inside the session.");
        if (r.EndDate is DateOnly e && (e < session.StartDate || e > session.EndDate)) return Error.Validation("Exam dates must be inside the session.");
        if (r.StartDate is DateOnly a && r.EndDate is DateOnly b && b < a) return Error.Validation("The end date cannot be before the start date.");
        return null;
    }

    private static void Apply(Exam exam, SaveExamRequest r)
    {
        exam.Name = r.Name.Trim();
        exam.Type = r.Type;
        exam.StartDate = r.StartDate;
        exam.EndDate = r.EndDate;
    }
}
