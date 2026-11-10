using System.Text.Json;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Exams;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Application.Services.Exams;

/// <summary>Printable exam / annual cards and a student's own results list.</summary>
public class ReportCardService(
    SchoolManagementSystemRepositories repositories,
    ExamAccess access,
    ResultService results,
    IAttendanceReportService attendance) : IReportCardService
{
    private static readonly Error NotAllowed = Error.Forbidden("You cannot see results for this section.");
    private static readonly Error NotPublished = Error.Validation("Publish the result first.");
    private static readonly Error PickOne = Error.Validation("Choose a section or a student.");

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public async Task<Result<IReadOnlyList<ReportCardDto>>> ExamCardsAsync(int examId, int? sectionId, int? studentId, CancellationToken ct = default)
    {
        if (sectionId == null && studentId == null) return PickOne;
        var exam = await repositories.Results.GetExamAsync(examId, ct);
        if (exam == null) return Error.NotFound("Exam");
        if (sectionId == null)
        {
            var snap = await repositories.Results.SnapshotAsync(examId, studentId!.Value, ct);
            if (snap == null) return NotPublished;
            sectionId = snap.SectionId;
        }

        var section = await repositories.Teaching.GetSectionAsync(sectionId.Value, ct);
        if (section == null || section.AcademicSessionId != exam.AcademicSessionId) return Error.NotFound("Section");
        if (!await access.CanSeeSectionAsync(section, AppModules.Results, ct)) return NotAllowed;
        var sr = await repositories.Results.SectionResultTrackedAsync(examId, section.Id, ct);
        if (sr is not { IsPublished: true }) return NotPublished;

        var snaps = (await repositories.Results.SnapshotsTrackedAsync(examId, section.Id, ct))
            .Where(s => studentId == null || s.StudentId == studentId).ToList();
        if (studentId != null && snaps.Count == 0) return NotPublished;
        var ids = snaps.Select(s => s.StudentId).ToList();
        var info = await InfoAsync(ids, section, ct);
        var remarks = (await repositories.Results.RemarksAsync(examId, exam.AcademicSessionId, ids, ct)).ToDictionary(r => r.StudentId, r => r.Remark);
        var (school, campus) = await HeaderAsync(section.CampusId, ct);
        // Spec: attendance from the session start up to the exam's start date.
        var to = (exam.StartDate ?? exam.EndDate) is DateOnly start && start < Today ? start : Today;

        var cards = new List<ReportCardDto>();
        foreach (var s in snaps)
        {
            var subjects = JsonSerializer.Deserialize<List<SubjectCalc>>(s.SubjectsJson) ?? [];
            cards.Add(new ReportCardDto(school, campus, exam.Name, exam.AcademicSession.Name, info[s.StudentId], subjects,
                s.Obtained, s.Total, s.Percent, s.Grade, s.Passed, s.Position,
                await attendance.AttendancePctAsync(s.StudentId, exam.AcademicSessionId, to, ct),
                remarks.GetValueOrDefault(s.StudentId), null, null));
        }

        return cards.OrderBy(c => c.Student.RollNo).ThenBy(c => c.Student.Name).ToList();
    }

    public async Task<Result<IReadOnlyList<ReportCardDto>>> AnnualCardsAsync(int sessionId, int? sectionId, int? studentId, CancellationToken ct = default)
    {
        if (sectionId == null && studentId == null) return PickOne;
        var session = await repositories.Sessions.GetTrackedAsync(sessionId, ct);
        if (session == null) return Error.NotFound("Session");
        var section = sectionId is int sid
            ? await repositories.Teaching.GetSectionAsync(sid, ct)
            : await repositories.Results.CurrentSectionAsync(studentId!.Value, sessionId, ct) is { } cur
                ? await repositories.Teaching.GetSectionAsync(cur.Id, ct)
                : null;
        if (section == null || section.AcademicSessionId != sessionId) return Error.NotFound("Section");
        if (!await access.CanSeeSectionAsync(section, AppModules.Results, ct)) return NotAllowed;

        var students = (await repositories.Results.SectionStudentsAsync(section.Id, sessionId, ct))
            .Where(s => studentId == null || s.StudentId == studentId).ToList();
        if (studentId != null && students.Count == 0) return Error.NotFound("Student");
        var ids = students.Select(s => s.StudentId).ToList();
        var annual = await results.AnnualForAsync(section.CampusId, sessionId, section.ClassId, ids, ct);
        var info = await InfoAsync(ids, section, ct);
        var remarks = (await repositories.Results.RemarksAsync(null, sessionId, ids, ct)).ToDictionary(r => r.StudentId, r => r.Remark);
        var (school, campus) = await HeaderAsync(section.CampusId, ct);

        var cards = new List<ReportCardDto>();
        foreach (var s in students)
        {
            var (a, exams) = annual[s.StudentId];
            cards.Add(new ReportCardDto(school, campus, "Annual Result", session.Name, info[s.StudentId], [], null, null, a.Percent, a.Grade, a.Passed,
                null, await attendance.AttendancePctAsync(s.StudentId, sessionId, Today, ct), remarks.GetValueOrDefault(s.StudentId), a, exams));
        }

        return cards;
    }

    public async Task<Result<StudentResultsDto>> StudentResultsAsync(int studentId, CancellationToken ct = default)
    {
        var student = (await repositories.Results.StudentsAsync([studentId], ct)).FirstOrDefault();
        if (student == null) return Error.NotFound("Student");
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(student.CampusId, ct);
        if (session == null) return new StudentResultsDto([], null);
        var section = await repositories.Results.CurrentSectionAsync(studentId, session.Id, ct);
        var allowed = section != null
            ? await access.CanSeeSectionAsync(section, AppModules.Results, ct)
            : await access.IsAdminAsync(AppModules.Results, ct) || await access.IsViewerAsync(ct);
        if (!allowed) return NotAllowed;

        var published = await repositories.Results.PublishedForStudentsAsync([studentId], session.Id, ct);
        var exams = published.OrderBy(p => p.StartDate).ThenBy(p => p.ExamId)
            .Select(p => new StudentExamResultDto(p.ExamId, p.ExamName, p.Type, p.Result.Percent, p.Result.Grade, p.Result.Passed, p.Result.Position))
            .ToList();
        AnnualCalc? annual = null;
        if (section != null)
            annual = (await results.AnnualForAsync(student.CampusId, session.Id, section.ClassId, [studentId], ct))[studentId].Result;
        return new StudentResultsDto(exams, annual);
    }

    // ---------------------------------------------------------------- helpers

    private async Task<Dictionary<int, StudentCardInfo>> InfoAsync(IReadOnlyList<int> ids, Section section, CancellationToken ct)
    {
        var students = (await repositories.Results.StudentsAsync(ids, ct)).ToDictionary(s => s.Id);
        var rolls = (await repositories.Results.NamesAsync(ids, section.Id, ct)).ToDictionary(n => n.StudentId);
        return ids.ToDictionary(id => id, id =>
        {
            students.TryGetValue(id, out var s);
            var r = rolls.GetValueOrDefault(id);
            return new StudentCardInfo(id, s?.FullName ?? r?.Name ?? "", s?.AdmissionNo ?? "", section.Class.Name, section.Name, r?.RollNo ?? 0,
                !string.IsNullOrEmpty(s?.PhotoPath));
        });
    }

    private async Task<(string School, string Campus)> HeaderAsync(int campusId, CancellationToken ct)
    {
        var school = await repositories.Schools.GetDtoAsync(ct);
        var campus = await repositories.Campuses.GetDtoAsync(campusId, ct);
        return (school?.Name ?? "", campus?.Name ?? "");
    }
}
