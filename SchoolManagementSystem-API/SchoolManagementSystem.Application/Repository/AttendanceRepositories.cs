using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Entities.Attendance;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

/// <summary>Weekly offs and holidays. Holidays are campus-owned rows (null = every campus).</summary>
public class CalendarRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<CampusCalendar?> GetTrackedAsync(int campusId, CancellationToken ct = default) =>
        Db.CampusCalendars.FirstOrDefaultAsync(c => c.CampusId == campusId, ct);

    /// <summary>Weekly off string of a campus, ignoring the request's campus filter (callers have checked access).</summary>
    public async Task<string> WeeklyOffAsync(int campusId, CancellationToken ct = default) =>
        await Db.CampusCalendars.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.CampusId == campusId && !c.IsDeleted).Select(c => c.WeeklyOff).FirstOrDefaultAsync(ct) ?? "Sunday";

    /// <summary>Holidays of a campus plus all-campus ones overlapping [from, to], ignoring the request filter.</summary>
    public Task<List<Holiday>> HolidaysAsync(int campusId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.Holidays.IgnoreQueryFilters().AsNoTracking()
            .Where(h => !h.IsDeleted && (h.CampusId == null || h.CampusId == campusId) && h.StartDate <= to && h.EndDate >= from)
            .OrderBy(h => h.StartDate)
            .ToListAsync(ct);

    public Task<List<Holiday>> ListForCampusAsync(int campusId, CancellationToken ct = default) =>
        Db.Holidays.AsNoTracking().Include(h => h.Campus)
            .Where(h => h.CampusId == null || h.CampusId == campusId)
            .OrderBy(h => h.StartDate)
            .ToListAsync(ct);

    public Task<Holiday?> GetHolidayAsync(int id, CancellationToken ct = default) =>
        Db.Holidays.Include(h => h.Campus).FirstOrDefaultAsync(h => h.Id == id, ct);
}

/// <summary>Student attendance days and entries (campus-filtered through the day / section).</summary>
public class AttendanceRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public record RosterRow(int StudentId, int RollNo, string Name, bool HasPhoto);

    /// <summary>
    /// Students on the section's sheet for a date, in roll order: those enrolled in it that day (minus anyone already
    /// marked in another section that day), plus anyone already marked in this section that day (moved away since).
    /// </summary>
    public async Task<List<RosterRow>> RosterAsync(int sectionId, DateOnly date, CancellationToken ct = default)
    {
        var enrolled = await Db.Enrollments.AsNoTracking()
            .Where(e => e.SectionId == sectionId && e.StartDate <= date && (e.EndDate == null || e.EndDate >= date) && !e.Student.IsDeleted
                        && !Db.AttendanceEntries.Any(x => x.StudentId == e.StudentId && x.AttendanceDay.Date == date
                                                          && x.AttendanceDay.SectionId != sectionId && !x.AttendanceDay.IsDeleted))
            .Select(e => new RosterRow(e.StudentId, e.RollNo, (e.Student.FirstName + " " + e.Student.LastName).Trim(), e.Student.PhotoPath != null))
            .ToListAsync(ct);
        var markedHere = await Db.AttendanceEntries.AsNoTracking()
            .Where(x => x.AttendanceDay.SectionId == sectionId && x.AttendanceDay.Date == date && !x.Student.IsDeleted)
            .Select(x => new RosterRow(x.StudentId,
                Db.Enrollments.Where(e => e.StudentId == x.StudentId && e.EndDate == null).Select(e => e.RollNo).FirstOrDefault(),
                (x.Student.FirstName + " " + x.Student.LastName).Trim(), x.Student.PhotoPath != null))
            .ToListAsync(ct);
        return enrolled.Concat(markedHere.Where(m => enrolled.All(e => e.StudentId != m.StudentId)))
            .OrderBy(r => r.RollNo).ThenBy(r => r.Name).ToList();
    }

    public Task<AttendanceDay?> GetDayTrackedAsync(int sectionId, DateOnly date, CancellationToken ct = default) =>
        Db.AttendanceDays.Include(d => d.Entries).FirstOrDefaultAsync(d => d.SectionId == sectionId && d.Date == date, ct);

    /// <summary>Read-only copy of the day for display (never the tracked instance).</summary>
    public Task<AttendanceDay?> GetDayAsync(int sectionId, DateOnly date, CancellationToken ct = default) =>
        Db.AttendanceDays.AsNoTracking().Include(d => d.Entries).FirstOrDefaultAsync(d => d.SectionId == sectionId && d.Date == date, ct);

    public Task<string?> UserNameAsync(int? userId, CancellationToken ct = default) =>
        userId == null
            ? Task.FromResult<string?>(null)
            : Db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => (u.FirstName + " " + u.LastName).Trim()).FirstOrDefaultAsync(ct);

    /// <summary>Section ids marked on a date (campus-filtered).</summary>
    public Task<List<int>> MarkedSectionIdsAsync(DateOnly date, CancellationToken ct = default) =>
        Db.AttendanceDays.AsNoTracking().Where(d => d.Date == date).Select(d => d.SectionId).ToListAsync(ct);

    public void RemoveEntries(IEnumerable<AttendanceEntry> entries) => Db.AttendanceEntries.RemoveRange(entries);
}

/// <summary>Read models for attendance reports (campus-filtered through days and enrollments).</summary>
public class AttendanceReportRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public record EntryRow(int SectionId, DateOnly Date, int StudentId, StudentAttendanceStatus Status, string? Note);

    public record StudentRow(int StudentId, int RollNo, string Name, int SectionId, string ClassName, string SectionName);

    /// <summary>Entries of the given sections between two dates.</summary>
    public Task<List<EntryRow>> EntriesAsync(IReadOnlyCollection<int> sectionIds, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.AttendanceEntries.AsNoTracking()
            .Where(e => sectionIds.Contains(e.AttendanceDay.SectionId) && e.AttendanceDay.Date >= from && e.AttendanceDay.Date <= to
                        && !e.AttendanceDay.IsDeleted)
            .Select(e => new EntryRow(e.AttendanceDay.SectionId, e.AttendanceDay.Date, e.StudentId, e.Status, e.Note))
            .ToListAsync(ct);

    /// <summary>Entries of one student in a session (campus-filtered through the day).</summary>
    public Task<List<EntryRow>> StudentEntriesAsync(int studentId, int sessionId, CancellationToken ct = default) =>
        Db.AttendanceEntries.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.AttendanceDay.AcademicSessionId == sessionId)
            .Join(Db.AttendanceDays, e => e.AttendanceDayId, d => d.Id, (e, d) => new EntryRow(d.SectionId, d.Date, e.StudentId, e.Status, e.Note))
            .ToListAsync(ct);

    /// <summary>Marked days (section, date, counts) of the given sections on a date.</summary>
    public Task<List<AttendanceDay>> DaysOnAsync(IReadOnlyCollection<int> sectionIds, DateOnly date, CancellationToken ct = default) =>
        Db.AttendanceDays.AsNoTracking().Where(d => sectionIds.Contains(d.SectionId) && d.Date == date).ToListAsync(ct);

    public Task<List<DateOnly>> MarkedDatesAsync(int sectionId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.AttendanceDays.AsNoTracking().Where(d => d.SectionId == sectionId && d.Date >= from && d.Date <= to).Select(d => d.Date).ToListAsync(ct);

    /// <summary>Students enrolled in any of the sections at some point in [from, to], with their section.</summary>
    public Task<List<StudentRow>> StudentsAsync(IReadOnlyCollection<int> sectionIds, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.Enrollments.AsNoTracking()
            .Where(e => sectionIds.Contains(e.SectionId) && e.StartDate <= to && (e.EndDate == null || e.EndDate >= from) && !e.Student.IsDeleted)
            .Select(e => new StudentRow(e.StudentId, e.RollNo, (e.Student.FirstName + " " + e.Student.LastName).Trim(), e.SectionId,
                e.Section.Class.Name, e.Section.Name))
            .ToListAsync(ct);

    /// <summary>Class teacher (staff id) of the section the student is currently enrolled in, if any.</summary>
    public Task<int?> CurrentClassTeacherAsync(int studentId, CancellationToken ct = default) =>
        Db.Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.EndDate == null)
            .Select(e => e.Section.ClassTeacherStaffId)
            .FirstOrDefaultAsync(ct);

    /// <summary>Names of students (campus-filtered) by id.</summary>
    public Task<Dictionary<int, string>> NamesAsync(IReadOnlyCollection<int> studentIds, CancellationToken ct = default) =>
        Db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => (s.FirstName + " " + s.LastName).Trim(), ct);
}

/// <summary>Staff attendance rows and the staff roster of a campus on a date.</summary>
public class StaffAttendanceRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public record StaffRow(int StaffId, string EmployeeNo, string Name, SchoolManagementSystem.Domain.Entities.Hiring.StaffDesignation Designation);

    /// <summary>Staff working on the campus at some point in [from, to]: joined by `to`, and not left before `from`.</summary>
    public Task<List<StaffRow>> RosterAsync(int campusId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.Staff.AsNoTracking()
            .Where(s => s.CampusId == campusId && s.JoiningDate <= to
                        && ((s.Status != SchoolManagementSystem.Domain.Entities.Hiring.StaffStatus.Resigned
                             && s.Status != SchoolManagementSystem.Domain.Entities.Hiring.StaffStatus.Terminated)
                            || (s.LeftOn != null && s.LeftOn >= from)))
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .Select(s => new StaffRow(s.Id, s.EmployeeNo, (s.FirstName + " " + s.LastName).Trim(), s.Designation))
            .ToListAsync(ct);

    public Task<List<StaffAttendance>> OnDateTrackedAsync(int campusId, DateOnly date, CancellationToken ct = default) =>
        Db.StaffAttendance.Where(a => a.CampusId == campusId && a.Date == date).ToListAsync(ct);

    public Task<List<StaffAttendance>> BetweenAsync(int campusId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.StaffAttendance.AsNoTracking().Where(a => a.CampusId == campusId && a.Date >= from && a.Date <= to).ToListAsync(ct);
}
