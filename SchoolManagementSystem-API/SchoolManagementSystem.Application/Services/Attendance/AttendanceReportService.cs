using System.Globalization;
using System.Text;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Attendance;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Attendance;

namespace SchoolManagementSystem.Application.Services.Attendance;

/// <summary>
/// Today, monthly sheet (+CSV), low attendance and a student's record. Admins see the campus; other users with
/// report access see only the sections they are class teacher of.
/// </summary>
public class AttendanceReportService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    CalendarService calendar,
    AttendanceAccess access) : IAttendanceReportService
{
    private static readonly Error OwnSectionsOnly = Error.Forbidden("You can only see the sections you are class teacher of.");

    public async Task<Result<TodayDto>> TodayAsync(CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var date = CalendarService.Today;
        var info = await calendar.IsWorkingDayAsync(campusId, date, ct);
        var sections = await VisibleSectionsAsync(campusId, ct);
        var ids = sections.Select(s => s.Id).ToList();
        var days = (await repositories.AttendanceReports.DaysOnAsync(ids, date, ct)).ToDictionary(d => d.SectionId);

        var rows = sections.Select(s =>
        {
            days.TryGetValue(s.Id, out var d);
            return new TodaySectionDto(s.Id, s.Class.Name, s.Name, s.ClassTeacher == null ? null : s.ClassTeacher.FullName,
                d != null, d?.Present ?? 0, d?.Absent ?? 0, d?.Late ?? 0, d?.Leave ?? 0);
        }).OrderBy(r => r.Marked).ThenBy(r => r.ClassName).ThenBy(r => r.SectionName).ToList();

        var entries = await repositories.AttendanceReports.EntriesAsync(days.Keys.ToList(), date, date, ct);
        var away = entries.Where(e => e.Status != StudentAttendanceStatus.Present).ToList();
        var names = await repositories.AttendanceReports.NamesAsync(away.Select(e => e.StudentId).Distinct().ToList(), ct);
        var bySection = sections.ToDictionary(s => s.Id);
        var absentees = away
            .Where(e => names.ContainsKey(e.StudentId))
            .Select(e => new AbsenteeDto(e.StudentId, names[e.StudentId], bySection[e.SectionId].Class.Name, bySection[e.SectionId].Name, e.Status))
            .OrderBy(x => x.Status).ThenBy(x => x.ClassName).ThenBy(x => x.Name).ToList();

        var marked = days.Values.ToList();
        var pct = AttendanceMath.Pct(marked.Sum(d => d.Present), marked.Sum(d => d.Late),
            marked.Sum(d => d.Present + d.Absent + d.Late + d.Leave), marked.Sum(d => d.Leave));
        return new TodayDto(date, info.IsWorking, info.Reason, info.IsWorking ? pct : null, rows, absentees);
    }

    public async Task<Result<MonthlyDto>> MonthlyAsync(int sectionId, string month, CancellationToken ct = default)
    {
        var built = await BuildMonthlyAsync(sectionId, month, ct);
        return built.IsSuccess ? built.Value!.Dto : built.Error!;
    }

    public async Task<Result<(byte[] Content, string FileName)>> MonthlyCsvAsync(int sectionId, string month, CancellationToken ct = default)
    {
        var built = await BuildMonthlyAsync(sectionId, month, ct);
        if (!built.IsSuccess) return built.Error!;
        var dto = built.Value!.Dto;

        var csv = new StringBuilder();
        csv.Append("Roll,Name,");
        csv.Append(string.Join(',', dto.Days.Select(d => d.Date.Day.ToString(CultureInfo.InvariantCulture))));
        csv.AppendLine(",Present,Absent,Late,Leave,%");
        foreach (var s in dto.Students)
        {
            csv.Append(s.RollNo).Append(',').Append(Csv(s.Name)).Append(',');
            csv.Append(string.Join(',', s.Cells));
            csv.Append(',').Append(s.Present).Append(',').Append(s.Absent).Append(',').Append(s.Late).Append(',').Append(s.Leave)
                .Append(',').AppendLine(s.Pct?.ToString("0.0", CultureInfo.InvariantCulture) ?? "");
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return (bytes, $"attendance-{Slug(dto.ClassName)}-{Slug(dto.SectionName)}-{dto.Month}.csv");
    }

    public async Task<Result<IReadOnlyList<LowDto>>> LowAsync(decimal threshold, int? sectionId, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        if (threshold is < 0 or > 100) return Error.Validation("The threshold must be between 0 and 100.");
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct);
        if (session == null) return Array.Empty<LowDto>();

        var sections = (await VisibleSectionsAsync(campusId, ct)).Where(s => sectionId == null || s.Id == sectionId).ToList();
        if (sectionId != null && sections.Count == 0) return OwnSectionsOnly;
        var to = CalendarService.Today < session.EndDate ? CalendarService.Today : session.EndDate;
        var kinds = await calendar.DayKindsAsync(campusId, session.StartDate, to, ct);
        var ids = sections.Select(s => s.Id).ToList();
        var entries = (await repositories.AttendanceReports.EntriesAsync(ids, session.StartDate, to, ct))
            .Where(e => kinds.TryGetValue(e.Date, out var k) && k.Kind != DayKind.Holiday).ToList();
        var students = (await repositories.AttendanceReports.StudentsAsync(ids, to, to, ct)).ToDictionary(s => s.StudentId);

        return entries.GroupBy(e => e.StudentId)
            .Where(g => students.ContainsKey(g.Key))
            .Select(g =>
            {
                var present = g.Count(e => e.Status == StudentAttendanceStatus.Present);
                var late = g.Count(e => e.Status == StudentAttendanceStatus.Late);
                var leave = g.Count(e => e.Status == StudentAttendanceStatus.Leave);
                var pct = AttendanceMath.Pct(present, late, g.Count(), leave);
                var s = students[g.Key];
                return (pct, dto: new LowDto(s.StudentId, s.Name, s.ClassName, s.SectionName, g.Count() - leave, present + late, pct ?? 0));
            })
            .Where(x => x.pct != null && x.pct < threshold)
            .Select(x => x.dto)
            .OrderBy(x => x.Pct).ThenBy(x => x.Name)
            .ToList();
    }

    public async Task<Result<StudentAttendanceDto>> StudentAsync(int studentId, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        if (student == null) return Error.NotFound("Student");
        if (await access.IsRestrictedAsync(ct)
            && (await access.MyStaffIdAsync(ct) is not int me || await repositories.AttendanceReports.CurrentClassTeacherAsync(studentId, ct) != me))
            return OwnSectionsOnly;
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(student.CampusId, ct);
        if (session == null) return new StudentAttendanceDto(null, 0, 0, 0, 0, 0, [], []);

        var entries = await repositories.AttendanceReports.StudentEntriesAsync(studentId, session.Id, ct);
        var to = CalendarService.Today < session.EndDate ? CalendarService.Today : session.EndDate;
        var kinds = await calendar.DayKindsAsync(student.CampusId, session.StartDate, to, ct);
        var counted = entries.Where(e => kinds.TryGetValue(e.Date, out var k) && k.Kind != DayKind.Holiday).ToList();

        static (int P, int A, int L, int Lv) Totals(IEnumerable<AttendanceReportRepository.EntryRow> rows)
        {
            var list = rows.ToList();
            return (list.Count(e => e.Status == StudentAttendanceStatus.Present), list.Count(e => e.Status == StudentAttendanceStatus.Absent),
                list.Count(e => e.Status == StudentAttendanceStatus.Late), list.Count(e => e.Status == StudentAttendanceStatus.Leave));
        }

        var t = Totals(counted);
        var months = counted.GroupBy(e => e.Date.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var m = Totals(g);
                return new MonthSummaryDto(g.Key, m.P, m.A, m.L, m.Lv, AttendanceMath.Pct(m.P, m.L, g.Count(), m.Lv));
            }).ToList();
        var absences = counted.Where(e => e.Status != StudentAttendanceStatus.Present)
            .OrderByDescending(e => e.Date)
            .Select(e => new AbsenceDto(e.Date, e.Status, e.Note)).ToList();
        return new StudentAttendanceDto(AttendanceMath.Pct(t.P, t.L, counted.Count, t.Lv), t.P, t.A, t.L, t.Lv, counted.Count, months, absences);
    }

    public async Task<decimal?> AttendancePctAsync(int studentId, int sessionId, DateOnly to, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(studentId, ct);
        var session = await repositories.Sessions.GetTrackedAsync(sessionId, ct);
        if (student == null || session == null) return null;
        if (to > session.EndDate) to = session.EndDate;
        if (to < session.StartDate) return null;
        var entries = await repositories.AttendanceReports.StudentEntriesAsync(studentId, sessionId, ct);
        var kinds = await calendar.DayKindsAsync(student.CampusId, session.StartDate, to, ct);
        var counted = entries.Where(e => e.Date <= to && kinds.TryGetValue(e.Date, out var k) && k.Kind != DayKind.Holiday).ToList();
        return AttendanceMath.Pct(counted.Count(e => e.Status == StudentAttendanceStatus.Present),
            counted.Count(e => e.Status == StudentAttendanceStatus.Late), counted.Count,
            counted.Count(e => e.Status == StudentAttendanceStatus.Leave));
    }

    // ---------------------------------------------------------------- helpers

    private sealed record Monthly(MonthlyDto Dto);

    private async Task<Result<Monthly>> BuildMonthlyAsync(int sectionId, string month, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
            return Error.Validation("Month must look like 2026-11.");
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (section == null) return Error.NotFound("Section");
        if (!await CanSeeAsync(section, ct)) return OwnSectionsOnly;
        var session = (await repositories.Sessions.GetTrackedAsync(section.AcademicSessionId, ct))!;

        var to = from.AddMonths(1).AddDays(-1);
        var kinds = await calendar.DayKindsAsync(section.CampusId, from, to, ct);
        var markedDates = (await repositories.AttendanceReports.MarkedDatesAsync(section.Id, from, to, ct)).ToHashSet();
        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i)).Select(d =>
        {
            var (kind, name) = kinds[d];
            if (kind == DayKind.WeeklyOff && markedDates.Contains(d)) kind = DayKind.Working; // marked before the weekly off changed
            if (kind == DayKind.Working && (d < session.StartDate || d > session.EndDate)) kind = DayKind.OutsideSession;
            return new MonthDayDto(d, kind, name, markedDates.Contains(d));
        }).ToList();

        var entries = await repositories.AttendanceReports.EntriesAsync([section.Id], from, to, ct);
        var enrolled = await repositories.AttendanceReports.StudentsAsync([section.Id], from, to, ct);
        var extraIds = entries.Select(e => e.StudentId).Distinct().Where(id => enrolled.All(s => s.StudentId != id)).ToList();
        var extraNames = await repositories.AttendanceReports.NamesAsync(extraIds, ct);
        var students = enrolled.Select(s => (s.StudentId, s.RollNo, s.Name))
            .Concat(extraIds.Where(extraNames.ContainsKey).Select(id => (StudentId: id, RollNo: 0, Name: extraNames[id])))
            .GroupBy(s => s.StudentId).Select(g => g.First())
            .OrderBy(s => s.RollNo == 0 ? int.MaxValue : s.RollNo).ThenBy(s => s.Name).ToList();

        var byStudent = entries.GroupBy(e => e.StudentId).ToDictionary(g => g.Key, g => g.ToDictionary(e => e.Date));
        var working = days.Where(d => d.Kind == DayKind.Working).Select(d => d.Date).ToHashSet();
        var rows = students.Select(s =>
        {
            byStudent.TryGetValue(s.StudentId, out var mine);
            mine ??= [];
            var cells = days.Select(d => mine.TryGetValue(d.Date, out var e) ? AttendanceMath.Code(e.Status) : "").ToList();
            var counted = mine.Values.Where(e => working.Contains(e.Date)).ToList();
            int P = counted.Count(e => e.Status == StudentAttendanceStatus.Present), A = counted.Count(e => e.Status == StudentAttendanceStatus.Absent),
                L = counted.Count(e => e.Status == StudentAttendanceStatus.Late), Lv = counted.Count(e => e.Status == StudentAttendanceStatus.Leave);
            return new MonthStudentDto(s.StudentId, s.RollNo, s.Name, cells, P, A, L, Lv, AttendanceMath.Pct(P, L, counted.Count, Lv));
        }).ToList();

        return new Monthly(new MonthlyDto(section.Id, section.Class.Name, section.Name, from.ToString("yyyy-MM"), days, rows));
    }

    /// <summary>Current-session active sections of the campus the caller may see in reports.</summary>
    private async Task<List<Section>> VisibleSectionsAsync(int campusId, CancellationToken ct)
    {
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct);
        if (session == null) return [];
        var sections = await repositories.Teaching.SectionsWithTeacherAsync(campusId, session.Id, ct);
        if (!await access.IsRestrictedAsync(ct)) return sections;
        var me = await access.MyStaffIdAsync(ct);
        return sections.Where(s => me != null && s.ClassTeacherStaffId == me).ToList();
    }

    private Task<bool> CanSeeAsync(Section section, CancellationToken ct) => access.CanSeeSectionAsync(section, ct);

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    private static string Slug(string value) => new(value.Where(char.IsLetterOrDigit).ToArray());
}
