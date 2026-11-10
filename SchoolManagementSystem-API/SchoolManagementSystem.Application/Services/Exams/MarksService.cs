using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Exams;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Application.Services.Exams;

/// <summary>Marks entry: the subject teacher enters and submits, admins enter, correct and return.</summary>
public class MarksService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    ExamAccess access,
    GradingService grading) : IMarksService
{
    private static readonly Error NotOpen = Error.Validation("Marks can be entered only while the exam is open.");
    private static readonly Error Published = Error.Validation("This result is published. Unpublish it first.");
    private static readonly Error NotTeacher = Error.Forbidden("Only the subject teacher or an admin can enter these marks.");
    private static readonly Error SubmittedLock = Error.Validation("Submitted. Ask an admin to return it.");

    public async Task<Result<IReadOnlyList<MyPaperDto>>> MyPapersAsync(int? examId, CancellationToken ct = default)
    {
        var papers = await repositories.Marks.PapersForAsync(examId, ct);
        var pairs = new List<(ExamPaper Paper, int SectionId)>();
        if (await access.IsAdminAsync(AppModules.Marks, ct))
        {
            foreach (var g in papers.GroupBy(p => (p.Exam.AcademicSessionId, p.ClassId, p.Exam.CampusId)))
            {
                var secs = await repositories.Exams.SectionsAsync(g.Key.AcademicSessionId, [g.Key.ClassId], g.Key.CampusId, ct);
                pairs.AddRange(from p in g from s in secs select (p, s.Id));
            }
        }
        else if (await access.MyStaffIdAsync(ct) is int me)
        {
            foreach (var a in await repositories.Marks.AssignmentsAsync(me, ct))
                pairs.AddRange(papers.Where(p => p.ClassId == a.ClassId && p.SubjectId == a.SubjectId && p.Exam.AcademicSessionId == a.SessionId)
                    .Select(p => (p, a.SectionId)));
        }

        var sectionMap = (await repositories.Marks.SectionsByIdsAsync(pairs.Select(x => x.SectionId).Distinct().ToList(), ct)).ToDictionary(s => s.Id);
        var progress = await repositories.Marks.PaperSectionsForAsync(pairs.Select(x => x.Paper.Id).Distinct().ToList(), ct);
        var published = (await repositories.Marks.PublishedForAsync(pairs.Select(x => x.Paper.ExamId).Distinct().ToList(), ct))
            .Select(r => (r.ExamId, r.SectionId)).ToHashSet();

        return pairs.Where(x => sectionMap.ContainsKey(x.SectionId)).Select(x =>
        {
            var s = sectionMap[x.SectionId];
            var st = progress.FirstOrDefault(p => p.ExamPaperId == x.Paper.Id && p.SectionId == s.Id)?.Status ?? PaperSectionStatus.NotStarted;
            return new MyPaperDto(x.Paper.ExamId, x.Paper.Exam.Name, x.Paper.Id, s.Id, s.Class.Name, s.Name, x.Paper.Subject.Name, st,
                published.Contains((x.Paper.ExamId, s.Id)));
        }).OrderBy(x => x.ExamName).ThenBy(x => x.ClassName).ThenBy(x => x.SectionName).ThenBy(x => x.SubjectName).ToList();
    }

    public async Task<Result<MarkSheetDto>> GetAsync(int paperId, int sectionId, CancellationToken ct = default)
    {
        var ctx = await LoadAsync(paperId, sectionId, ct);
        if (ctx.Error != null) return ctx.Error;
        var (paper, section) = (ctx.Paper!, ctx.Section!);
        if (!await access.CanSeeSectionAsync(section, AppModules.Marks, ct) && !await access.TeachesAsync(sectionId, paper.SubjectId, ct))
            return NotTeacher;

        var roster = await repositories.Marks.RosterAsync(paperId, sectionId, RosterDate(paper.Exam), ct);
        var marks = (await repositories.Marks.MarksTrackedAsync(paperId, sectionId, ct)).ToDictionary(m => m.StudentId);
        var bands = await grading.BandsForAsync(section.CampusId, ct);
        var ps = await repositories.Marks.PaperSectionTrackedAsync(paperId, sectionId, ct);
        var status = ps?.Status ?? PaperSectionStatus.NotStarted;
        var published = await repositories.Marks.IsPublishedAsync(paper.ExamId, sectionId, ct);
        var editError = await EditErrorAsync(paper, sectionId, status, published, ct);
        var isAdmin = await access.IsAdminAsync(AppModules.Marks, ct);

        var rows = roster.Select(r =>
        {
            if (!marks.TryGetValue(r.StudentId, out var m)) return new MarkRowDto(r.StudentId, r.RollNo, r.Name, null, null, null, null, null);
            decimal? got = m.Status switch
            {
                MarkStatus.Present => (m.Theory ?? 0) + (m.Practical ?? 0),
                MarkStatus.Absent => 0,
                _ => null
            };
            var grade = got is decimal g ? ResultCalculator.GradeFor(ResultCalculator.Round(g * 100 / paper.TotalMarks), bands).Grade : null;
            return new MarkRowDto(r.StudentId, r.RollNo, r.Name, m.Theory, m.Practical, m.Status, got, grade);
        }).ToList();

        return new MarkSheetDto(ExamService.ToPaper(paper), paper.ExamId, paper.Exam.Name, paper.Exam.Status, section.Id, section.Class.Name,
            section.Name, status, published, editError == null, isAdmin && status == PaperSectionStatus.Submitted && !published,
            editError?.Message, bands, rows);
    }

    public async Task<Result<MarkSheetDto>> SaveAsync(int paperId, int sectionId, SaveMarksRequest request, CancellationToken ct = default)
    {
        var ctx = await LoadAsync(paperId, sectionId, ct);
        if (ctx.Error != null) return ctx.Error;
        var (paper, section) = (ctx.Paper!, ctx.Section!);
        var ps = await repositories.Marks.PaperSectionTrackedAsync(paperId, sectionId, ct);
        var published = await repositories.Marks.IsPublishedAsync(paper.ExamId, sectionId, ct);
        var editError = await EditErrorAsync(paper, sectionId, ps?.Status ?? PaperSectionStatus.NotStarted, published, ct);
        if (editError != null) return editError;

        var rows = request.Rows ?? [];
        if (rows.Count == 0) return Error.Validation("Nothing to save.");
        if (rows.Select(r => r.StudentId).Distinct().Count() != rows.Count) return Error.Validation("A student is listed twice.");
        var roster = (await repositories.Marks.RosterAsync(paperId, sectionId, RosterDate(paper.Exam), ct)).Select(r => r.StudentId).ToHashSet();
        if (rows.Any(r => !roster.Contains(r.StudentId))) return Error.Validation("Someone in the list is not in this section for this exam.");
        foreach (var r in rows)
        {
            var error = ValidateRow(paper, r);
            if (error != null) return error;
        }

        var existing = (await repositories.Marks.MarksTrackedAsync(paperId, sectionId, ct)).ToDictionary(m => m.StudentId);
        foreach (var r in rows)
        {
            if (!existing.TryGetValue(r.StudentId, out var m))
            {
                m = new Mark { CampusId = section.CampusId, ExamPaperId = paperId, SectionId = sectionId, StudentId = r.StudentId };
                repositories.Marks.Add(m);
            }

            m.Status = r.Status;
            m.Theory = r.Status == MarkStatus.Present ? r.Theory : null;
            m.Practical = r.Status == MarkStatus.Present && paper.HasPractical ? r.Practical : null;
        }

        if (ps == null)
        {
            ps = new PaperSection { CampusId = section.CampusId, ExamPaperId = paperId, SectionId = sectionId };
            repositories.Marks.Add(ps);
        }

        if (ps.Status != PaperSectionStatus.Submitted) ps.Status = PaperSectionStatus.InProgress;
        await repositories.SaveChangesAsync(ct); // the (paper, student) unique index stops a racing duplicate → 409
        return await GetAsync(paperId, sectionId, ct);
    }

    public async Task<Result<MarkSheetDto>> SubmitAsync(int paperId, int sectionId, CancellationToken ct = default)
    {
        var ctx = await LoadAsync(paperId, sectionId, ct);
        if (ctx.Error != null) return ctx.Error;
        var paper = ctx.Paper!;
        var ps = await repositories.Marks.PaperSectionTrackedAsync(paperId, sectionId, ct);
        var published = await repositories.Marks.IsPublishedAsync(paper.ExamId, sectionId, ct);
        var editError = await EditErrorAsync(paper, sectionId, ps?.Status ?? PaperSectionStatus.NotStarted, published, ct);
        if (editError != null) return editError;

        var roster = await repositories.Marks.RosterAsync(paperId, sectionId, RosterDate(paper.Exam), ct);
        var entered = (await repositories.Marks.MarksTrackedAsync(paperId, sectionId, ct)).Select(m => m.StudentId).ToHashSet();
        var missing = roster.Count(r => !entered.Contains(r.StudentId));
        if (missing > 0) return Error.Validation($"{missing} {(missing == 1 ? "student has" : "students have")} no marks yet.");
        if (roster.Count == 0) return Error.Validation("There are no students to submit.");

        ps!.Status = PaperSectionStatus.Submitted;
        ps.SubmittedByUserId = currentUser.UserId;
        ps.SubmittedOn = DateTime.UtcNow;
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(paperId, sectionId, ct);
    }

    public async Task<Result<MarkSheetDto>> ReturnAsync(int paperId, int sectionId, CancellationToken ct = default)
    {
        var ctx = await LoadAsync(paperId, sectionId, ct);
        if (ctx.Error != null) return ctx.Error;
        if (!await access.IsAdminAsync(AppModules.Marks, ct)) return Error.Forbidden("Only an admin can return marks to the teacher.");
        if (await repositories.Marks.IsPublishedAsync(ctx.Paper!.ExamId, sectionId, ct)) return Published;
        var ps = await repositories.Marks.PaperSectionTrackedAsync(paperId, sectionId, ct);
        if (ps == null || ps.Status != PaperSectionStatus.Submitted) return Error.Validation("These marks are not submitted.");
        ps.Status = PaperSectionStatus.InProgress;
        ps.SubmittedByUserId = null;
        ps.SubmittedOn = null;
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(paperId, sectionId, ct);
    }

    // ---------------------------------------------------------------- helpers

    internal static DateOnly RosterDate(Exam exam)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return exam.EndDate is DateOnly end && end < today ? end : today;
    }

    private async Task<(ExamPaper? Paper, Section? Section, Error? Error)> LoadAsync(int paperId, int sectionId, CancellationToken ct)
    {
        var paper = await repositories.Marks.GetPaperAsync(paperId, ct);
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (paper == null || section == null || section.ClassId != paper.ClassId || section.AcademicSessionId != paper.Exam.AcademicSessionId)
            return (null, null, Error.NotFound("Paper"));
        return (paper, section, null);
    }

    /// <summary>Null when the caller may change these marks now; else why not.</summary>
    private async Task<Error?> EditErrorAsync(ExamPaper paper, int sectionId, PaperSectionStatus status, bool published, CancellationToken ct)
    {
        if (paper.Exam.Status != ExamStatus.Open || !paper.IsActive) return NotOpen;
        if (published) return Published;
        if (await access.IsAdminAsync(AppModules.Marks, ct)) return null;
        if (!await access.CanAsync(AppModules.Marks, AppActions.Edit, ct) || !await access.TeachesAsync(sectionId, paper.SubjectId, ct)) return NotTeacher;
        return status == PaperSectionStatus.Submitted ? SubmittedLock : null;
    }

    private static Error? ValidateRow(ExamPaper paper, MarkRowRequest r)
    {
        if (!Enum.IsDefined(r.Status)) return Error.Validation("Unknown status.");
        if (r.Status != MarkStatus.Present) return null;
        if (r.Theory is not decimal t) return Error.Validation("Enter theory marks or mark the student absent.");
        if (t < 0 || t > paper.TheoryMarks || decimal.Round(t, 2) != t) return Error.Validation($"Theory marks must be between 0 and {paper.TheoryMarks:0.##}.");
        if (!paper.HasPractical && r.Practical != null) return Error.Validation("This paper has no practical.");
        if (paper.HasPractical)
        {
            if (r.Practical is not decimal p) return Error.Validation("Enter practical marks.");
            if (p < 0 || p > paper.PracticalMarks || decimal.Round(p, 2) != p) return Error.Validation($"Practical marks must be between 0 and {paper.PracticalMarks:0.##}.");
        }

        return null;
    }
}
