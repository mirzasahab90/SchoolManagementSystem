using System.Text;
using System.Text.Json;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Exams;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Application.Services.Exams;

/// <summary>Section tabulation (live until published, then the stored snapshot), publish / unpublish, CSV and remarks.</summary>
public class ResultService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    ExamAccess access,
    GradingService grading) : IResultService
{
    private static readonly Error NotAllowed = Error.Forbidden("You cannot see results for this section.");
    private static readonly Error NotAdmin = Error.Forbidden("Only an admin can publish or unpublish results.");

    public async Task<Result<TabulationDto>> TabulateAsync(int examId, int sectionId, CancellationToken ct = default)
    {
        var ctx = await LoadAsync(examId, sectionId, ct);
        if (ctx.Error != null) return ctx.Error;
        if (!await access.CanSeeSectionAsync(ctx.Section!, AppModules.Results, ct)) return NotAllowed;
        return await BuildAsync(ctx.Exam!, ctx.Section!, ctx.Papers!, ct);
    }

    public async Task<Result<(byte[] Content, string FileName)>> CsvAsync(int examId, int sectionId, CancellationToken ct = default)
    {
        var tab = await TabulateAsync(examId, sectionId, ct);
        if (!tab.IsSuccess) return tab.Error!;
        var t = tab.Value!;
        var csv = new StringBuilder();
        csv.Append("Roll,Name,");
        foreach (var s in t.Subjects) csv.Append(Csv(s)).Append(',');
        csv.Append("Obtained,Total,Percent,Grade,Result,Position\r\n");
        foreach (var r in t.Rows)
        {
            csv.Append(r.RollNo).Append(',').Append(Csv(r.Name)).Append(',');
            foreach (var name in t.Subjects)
            {
                var s = r.Subjects.FirstOrDefault(x => x.Name == name);
                csv.Append(s?.Status switch
                {
                    MarkStatus.Absent => "Absent",
                    MarkStatus.Exempted => "Exempted",
                    _ => s?.Obtained?.ToString("0.##") ?? ""
                }).Append(',');
            }

            csv.Append(r.Obtained?.ToString("0.##")).Append(',').Append(r.Total?.ToString("0.##")).Append(',')
                .Append(r.Percent?.ToString("0.00")).Append(',').Append(Csv(r.Grade ?? "")).Append(',')
                .Append(r.Passed switch { true => "Pass", false => "Fail", _ => "" }).Append(',')
                .Append(r.Position).Append("\r\n");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return (bytes, $"result-{Slug(t.ExamName)}-{Slug(t.ClassName)}-{Slug(t.SectionName)}.csv");
    }

    public async Task<Result<TabulationDto>> PublishAsync(int examId, int sectionId, CancellationToken ct = default)
    {
        var ctx = await LoadAsync(examId, sectionId, ct);
        if (ctx.Error != null) return ctx.Error;
        var (exam, section, papers) = (ctx.Exam!, ctx.Section!, ctx.Papers!);
        if (!await access.IsAdminAsync(AppModules.Results, ct)) return NotAdmin;
        if (exam.Status == ExamStatus.Draft) return Error.Validation("Open the exam before publishing results.");
        if (papers.Count == 0) return Error.Validation("This class has no active papers in this exam.");

        var sr = await repositories.Results.SectionResultTrackedAsync(examId, sectionId, ct);
        if (sr?.IsPublished == true) return Error.Validation("This result is already published.");

        var pending = await PendingAsync(papers, sectionId, ct);
        if (pending.Count > 0) return Error.Validation($"Marks are not submitted for: {string.Join(", ", pending)}.");

        var live = await BuildAsync(exam, section, papers, ct);
        foreach (var old in await repositories.Results.SnapshotsTrackedAsync(examId, sectionId, ct)) repositories.Results.Remove(old);
        foreach (var r in live.Rows)
        {
            repositories.Results.Add(new StudentResult
            {
                CampusId = section.CampusId, ExamId = examId, SectionId = sectionId, StudentId = r.StudentId,
                Obtained = r.Obtained, Total = r.Total, Percent = r.Percent, Grade = r.Grade, Passed = r.Passed, Position = r.Position,
                SubjectsJson = JsonSerializer.Serialize(r.Subjects)
            });
        }

        if (sr == null)
        {
            sr = new SectionResult { CampusId = section.CampusId, ExamId = examId, SectionId = sectionId };
            repositories.Results.Add(sr);
        }

        sr.IsPublished = true;
        sr.PublishedOn = DateTime.UtcNow;
        sr.PublishedByUserId = currentUser.UserId;
        await repositories.SaveChangesAsync(ct); // the (exam, section) unique index stops a racing first publish
        return await BuildAsync(exam, section, papers, ct);
    }

    public async Task<Result<TabulationDto>> UnpublishAsync(int examId, int sectionId, CancellationToken ct = default)
    {
        var ctx = await LoadAsync(examId, sectionId, ct);
        if (ctx.Error != null) return ctx.Error;
        if (!await access.IsAdminAsync(AppModules.Results, ct)) return NotAdmin;
        var sr = await repositories.Results.SectionResultTrackedAsync(examId, sectionId, ct);
        if (sr is not { IsPublished: true }) return Error.Validation("This result is not published.");
        foreach (var old in await repositories.Results.SnapshotsTrackedAsync(examId, sectionId, ct)) repositories.Results.Remove(old);
        sr.IsPublished = false;
        sr.PublishedOn = null;
        sr.PublishedByUserId = null;
        await repositories.SaveChangesAsync(ct);
        return await BuildAsync(ctx.Exam!, ctx.Section!, ctx.Papers!, ct);
    }

    public async Task<Result> SaveRemarkAsync(SaveRemarkRequest request, CancellationToken ct = default)
    {
        var text = (request.Remark ?? "").Trim();
        if (text.Length > 300) return Error.Validation("The remark can be at most 300 characters.");
        if (request.ExamId is int examId)
        {
            var exam = await repositories.Results.GetExamAsync(examId, ct);
            if (exam == null || exam.AcademicSessionId != request.SessionId) return Error.NotFound("Exam");
        }

        var section = await repositories.Results.CurrentSectionAsync(request.StudentId, request.SessionId, ct);
        if (section == null) return Error.NotFound("Student");
        if (!await access.IsAdminAsync(AppModules.Results, ct)
            && !(await access.MyStaffIdAsync(ct) is int me && section.ClassTeacherStaffId == me))
            return Error.Forbidden("Only the class teacher or an admin can write this remark.");

        var remark = await repositories.Results.RemarkTrackedAsync(request.ExamId, request.SessionId, request.StudentId, ct);
        if (text.Length == 0)
        {
            if (remark != null) repositories.Results.Remove(remark);
        }
        else if (remark == null)
        {
            repositories.Results.Add(new ResultRemark
            {
                CampusId = section.CampusId, ExamId = request.ExamId, AcademicSessionId = request.SessionId,
                StudentId = request.StudentId, Remark = text
            });
        }
        else
        {
            remark.Remark = text;
        }

        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Remark saved.");
    }

    public async Task<Result<IReadOnlyList<AnnualRowDto>>> AnnualAsync(int sessionId, int sectionId, CancellationToken ct = default)
    {
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (section == null || section.AcademicSessionId != sessionId) return Error.NotFound("Section");
        if (!await access.CanSeeSectionAsync(section, AppModules.Results, ct)) return NotAllowed;
        var students = await repositories.Results.SectionStudentsAsync(sectionId, sessionId, ct);
        var annual = await AnnualForAsync(section.CampusId, sessionId, section.ClassId, students.Select(s => s.StudentId).ToList(), ct);
        return students.Select(s => new AnnualRowDto(s.StudentId, s.RollNo, s.Name, annual[s.StudentId].Result, annual[s.StudentId].Exams)).ToList();
    }

    /// <summary>Annual result per student from the session's published snapshots (live; no annual snapshot).</summary>
    public async Task<Dictionary<int, (AnnualCalc Result, IReadOnlyList<ExamPercentDto> Exams)>> AnnualForAsync(
        int campusId, int sessionId, int classId, IReadOnlyList<int> studentIds, CancellationToken ct = default)
    {
        var scheme = await grading.SchemeForAsync(campusId, sessionId, classId, ct);
        var bands = await grading.BandsForAsync(campusId, ct);
        var published = (await repositories.Results.PublishedForStudentsAsync(studentIds, sessionId, ct)).ToLookup(p => p.Result.StudentId);
        var result = new Dictionary<int, (AnnualCalc, IReadOnlyList<ExamPercentDto>)>();
        foreach (var sid in studentIds)
        {
            var mine = published[sid].OrderBy(p => p.StartDate).ThenBy(p => p.ExamId).ToList();
            var byType = mine.Where(p => p.Result.Percent != null).GroupBy(p => p.Type)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<decimal>)g.Select(p => p.Result.Percent!.Value).ToList());
            // No published final → the must-pass-final rule has nothing to judge.
            var passedFinal = mine.Where(p => p.Type == ExamType.Final).All(p => p.Result.Passed == true);
            result[sid] = (AnnualCalculator.Compute(byType, scheme, passedFinal, bands),
                mine.Select(p => new ExamPercentDto(p.ExamId, p.ExamName, p.Type, p.Result.Percent)).ToList());
        }

        return result;
    }

    // ---------------------------------------------------------------- helpers

    private async Task<(Exam? Exam, Section? Section, List<ExamPaper>? Papers, Error? Error)> LoadAsync(
        int examId, int sectionId, CancellationToken ct)
    {
        var exam = await repositories.Results.GetExamAsync(examId, ct);
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (exam == null || section == null || section.AcademicSessionId != exam.AcademicSessionId
            || (exam.CampusId is int owner && owner != section.CampusId)
            || exam.Papers.All(p => p.ClassId != section.ClassId))
            return (null, null, null, Error.NotFound("Result"));
        var papers = exam.Papers.Where(p => p.IsActive && p.ClassId == section.ClassId).OrderBy(p => p.Subject.Name).ToList();
        return (exam, section, papers, null);
    }

    private async Task<List<string>> PendingAsync(List<ExamPaper> papers, int sectionId, CancellationToken ct)
    {
        var progress = await repositories.Marks.PaperSectionsForAsync(papers.Select(p => p.Id).ToList(), ct);
        return papers
            .Where(p => progress.FirstOrDefault(x => x.ExamPaperId == p.Id && x.SectionId == sectionId)?.Status != PaperSectionStatus.Submitted)
            .Select(p => p.Subject.Name).ToList();
    }

    private async Task<TabulationDto> BuildAsync(Exam exam, Section section, List<ExamPaper> papers, CancellationToken ct)
    {
        var sr = await repositories.Results.SectionResultTrackedAsync(exam.Id, section.Id, ct);
        if (sr is { IsPublished: true })
        {
            var snaps = await repositories.Results.SnapshotsTrackedAsync(exam.Id, section.Id, ct);
            var names = (await repositories.Results.NamesAsync(snaps.Select(s => s.StudentId).ToList(), section.Id, ct))
                .ToDictionary(n => n.StudentId);
            var rows = snaps.Select(s =>
            {
                var n = names.GetValueOrDefault(s.StudentId);
                var subjects = JsonSerializer.Deserialize<List<SubjectCalc>>(s.SubjectsJson) ?? [];
                return new TabRowDto(s.StudentId, n?.RollNo ?? 0, n?.Name ?? "", subjects, s.Obtained, s.Total, s.Percent, s.Grade, s.Passed, s.Position);
            }).OrderBy(r => r.RollNo).ThenBy(r => r.Name).ToList();
            var subjectNames = rows.SelectMany(r => r.Subjects.Select(x => x.Name)).Distinct().ToList();
            return new TabulationDto(exam.Id, exam.Name, section.Id, section.Class.Name, section.Name, true, sr.PublishedOn, subjectNames, rows, []);
        }

        // One tabulation per student per exam: a student moved between sections during the exam belongs to the section they are
        // enrolled in on the roster date, and their marks are combined from every section they sat papers in.
        var date = MarksService.RosterDate(exam);
        var roster = new Dictionary<int, MarkRepository.RosterRow>();
        foreach (var r in await repositories.Results.EnrolledOnAsync(section.Id, date, ct)) roster.TryAdd(r.StudentId, r);
        foreach (var p in papers)
            foreach (var r in await repositories.Marks.RosterAsync(p.Id, section.Id, date, ct))
                roster.TryAdd(r.StudentId, r);
        var ids = roster.Keys.ToList();
        var home = await repositories.Results.EnrolledSectionOnAsync(ids, section.ClassId, section.AcademicSessionId, date, ct);
        var publishedElsewhere = await repositories.Results.PublishedElsewhereAsync(exam.Id, section.Id, ids, ct);
        foreach (var id in ids.Where(id => publishedElsewhere.Contains(id) || (home.TryGetValue(id, out var h) && h != section.Id)))
            roster.Remove(id);

        var marks = await repositories.Results.MarksForStudentsAsync(papers.Select(p => p.Id).ToList(), roster.Keys.ToList(), ct);
        var calcs = ResultCalculator.Compute(
            papers.Select(p => new PaperCalc(p.Id, p.SubjectId, p.Subject.Name, p.TotalMarks, p.PassMarks, p.HasPractical)).ToList(),
            roster.Keys.ToList(),
            marks.Select(m => new MarkCalc(m.ExamPaperId, m.StudentId, m.Theory, m.Practical, m.Status)).ToList(),
            await grading.BandsForAsync(section.CampusId, ct));

        var liveRows = calcs.Select(c =>
        {
            var r = roster[c.StudentId];
            return new TabRowDto(c.StudentId, r.RollNo, r.Name, c.Subjects, c.Obtained, c.Total, c.Percent, c.Grade, c.Passed, c.Position);
        }).OrderBy(r => r.RollNo).ThenBy(r => r.Name).ToList();
        return new TabulationDto(exam.Id, exam.Name, section.Id, section.Class.Name, section.Name, false, null,
            papers.Select(p => p.Subject.Name).ToList(), liveRows, await PendingAsync(papers, section.Id, ct));
    }

    /// <summary>Quotes when needed and defuses spreadsheet formulas.</summary>
    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private static string Slug(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
}
