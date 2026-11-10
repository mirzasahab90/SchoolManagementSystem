using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Attendance;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Attendance;

namespace SchoolManagementSystem.Application.Services.Attendance;

/// <summary>Daily section attendance: the sheet for a date, and saving it.</summary>
public class AttendanceService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    CalendarService calendar,
    AttendanceAccess access) : IAttendanceService
{
    public async Task<Result<IReadOnlyList<MySectionDto>>> MySectionsAsync(CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct);
        if (session == null) return Array.Empty<MySectionDto>();

        var sections = await repositories.Teaching.SectionsAsync(campusId, session.Id, ct);
        var admin = await access.IsAdminAsync(ct);
        var me = await access.MyStaffIdAsync(ct);
        var marked = (await repositories.Attendance.MarkedSectionIdsAsync(CalendarService.Today, ct)).ToHashSet();
        return sections
            .Where(s => admin || (me != null && s.ClassTeacherStaffId == me))
            .Select(s => new MySectionDto(s.Id, s.Class.Name, s.Name, me != null && s.ClassTeacherStaffId == me, marked.Contains(s.Id)))
            .ToList();
    }

    public async Task<Result<SheetDto>> GetSheetAsync(int sectionId, DateOnly date, CancellationToken ct = default)
    {
        var section = await repositories.Teaching.GetSectionAsync(sectionId, ct);
        if (section == null) return Error.NotFound("Section");
        var session = await repositories.Sessions.GetTrackedAsync(section.AcademicSessionId, ct);

        if (!await access.CanSeeSectionAsync(section, ct)) return Error.Forbidden(AttendanceAccess.NotClassTeacher);
        var dayError = await CheckDayAsync(section, session!, date, ct);
        var acc = await access.ResolveAsync(section, date, ct);
        var roster = await repositories.Attendance.RosterAsync(sectionId, date, ct);
        var day = await repositories.Attendance.GetDayAsync(sectionId, date, ct);
        var saved = day?.Entries.GroupBy(e => e.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.Id).First()) ?? [];

        var rows = roster.Select(r => saved.TryGetValue(r.StudentId, out var e)
            ? new SheetRowDto(r.StudentId, r.RollNo, r.Name, r.HasPhoto, e.Status, e.Note)
            : new SheetRowDto(r.StudentId, r.RollNo, r.Name, r.HasPhoto, StudentAttendanceStatus.Present, null)).ToList();

        var canEdit = dayError == null && acc.CanEdit;
        return new SheetDto(
            section.Id, section.Class.Name, section.Name, date, dayError == null, dayError?.Message, canEdit,
            canEdit ? null : dayError?.Message ?? acc.LockedReason,
            day != null, await repositories.Attendance.UserNameAsync(day?.MarkedByUserId, ct), day?.MarkedOn, rows);
    }

    public async Task<Result<SheetDto>> SaveSheetAsync(SaveSheetRequest request, CancellationToken ct = default)
    {
        var section = await repositories.Teaching.GetSectionAsync(request.SectionId, ct);
        if (section == null) return Error.NotFound("Section");
        var session = await repositories.Sessions.GetTrackedAsync(section.AcademicSessionId, ct);

        var dayError = await CheckDayAsync(section, session!, request.Date, ct);
        if (dayError != null) return dayError;
        var acc = await access.ResolveAsync(section, request.Date, ct);
        if (!acc.CanEdit) return Error.Forbidden(acc.LockedReason ?? AttendanceAccess.NotClassTeacher);

        var entries = request.Entries ?? [];
        var roster = await repositories.Attendance.RosterAsync(section.Id, request.Date, ct);
        var rosterIds = roster.Select(r => r.StudentId).ToHashSet();
        var sentIds = entries.Select(e => e.StudentId).ToList();
        if (sentIds.Count != sentIds.Distinct().Count() || !rosterIds.SetEquals(sentIds))
            return Error.Validation($"The list does not match the students of this section on {CalendarService.FormatDate(request.Date)}.");
        if (entries.Any(e => !Enum.IsDefined(e.Status))) return Error.Validation("Unknown status.");
        if (entries.Any(e => e.Note?.Trim().Length > 200)) return Error.Validation("Notes are limited to 200 characters.");

        var day = await repositories.Attendance.GetDayTrackedAsync(section.Id, request.Date, ct);
        if (day == null)
        {
            day = new AttendanceDay
            {
                CampusId = section.CampusId,
                SectionId = section.Id,
                AcademicSessionId = section.AcademicSessionId,
                Date = request.Date
            };
            repositories.Attendance.Add(day);
        }
        else
        {
            // Replace only the roster's entries; students who moved away since keep their entry for this day.
            var replaced = day.Entries.Where(e => rosterIds.Contains(e.StudentId)).ToList();
            repositories.Attendance.RemoveEntries(replaced);
            foreach (var e in replaced) day.Entries.Remove(e);
        }

        foreach (var e in entries)
        {
            day.Entries.Add(new AttendanceEntry
            {
                StudentId = e.StudentId,
                Status = e.Status,
                Note = string.IsNullOrWhiteSpace(e.Note) ? null : e.Note.Trim()
            });
        }

        day.Present = day.Entries.Count(e => e.Status == StudentAttendanceStatus.Present);
        day.Absent = day.Entries.Count(e => e.Status == StudentAttendanceStatus.Absent);
        day.Late = day.Entries.Count(e => e.Status == StudentAttendanceStatus.Late);
        day.Leave = day.Entries.Count(e => e.Status == StudentAttendanceStatus.Leave);
        day.MarkedByUserId = currentUser.UserId;
        day.MarkedOn = DateTime.UtcNow;
        await repositories.SaveChangesAsync(ct); // a racing first save hits the (section, date) unique index → 409

        return await GetSheetAsync(section.Id, request.Date, ct);
    }

    /// <summary>Null when attendance can be taken for the section on the date; else the reason.</summary>
    private async Task<Error?> CheckDayAsync(Section section, AcademicSession session, DateOnly date, CancellationToken ct)
    {
        if (session.Status == SessionStatus.Closed) return Error.Validation("This session is closed.");
        if (date < session.StartDate || date > session.EndDate)
            return Error.Validation($"{CalendarService.FormatDate(date)} is outside the session {session.Name}.");
        var info = await calendar.IsWorkingDayAsync(section.CampusId, date, ct);
        return info.IsWorking ? null : Error.Validation(info.Reason!);
    }
}
