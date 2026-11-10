using System.Globalization;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Attendance;
using SchoolManagementSystem.Domain.Entities.Attendance;

namespace SchoolManagementSystem.Application.Services.Attendance;

/// <summary>Daily staff attendance taken by the office, and the monthly staff sheet.</summary>
public class StaffAttendanceService(
    SchoolManagementSystemRepositories repositories,
    ICurrentUser currentUser,
    CalendarService calendar) : IStaffAttendanceService
{
    public async Task<Result<StaffSheetDto>> GetAsync(DateOnly date, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var info = await calendar.IsWorkingDayAsync(campusId, date, ct);
        var roster = await repositories.StaffAttendance.RosterAsync(campusId, date, date, ct);
        var saved = (await repositories.StaffAttendance.OnDateTrackedAsync(campusId, date, ct)).ToDictionary(a => a.StaffId);
        var rows = roster.Select(s => saved.TryGetValue(s.StaffId, out var a)
            ? new StaffSheetRowDto(s.StaffId, s.EmployeeNo, s.Name, s.Designation, a.Status, a.CheckIn, a.Note, true)
            : new StaffSheetRowDto(s.StaffId, s.EmployeeNo, s.Name, s.Designation, null, null, null, false)).ToList();
        return new StaffSheetDto(date, info.IsWorking, info.Reason, rows);
    }

    public async Task<Result<StaffSheetDto>> SaveAsync(SaveStaffSheetRequest request, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var info = await calendar.IsWorkingDayAsync(campusId, request.Date, ct);
        if (!info.IsWorking) return Error.Validation(info.Reason!);

        var entries = request.Entries ?? [];
        if (entries.Count == 0) return Error.Validation("Nothing to save.");
        if (entries.Select(e => e.StaffId).Distinct().Count() != entries.Count) return Error.Validation("A staff member is listed twice.");
        if (entries.Any(e => !Enum.IsDefined(e.Status))) return Error.Validation("Unknown status.");
        if (entries.Any(e => e.Note?.Trim().Length > 200)) return Error.Validation("Notes are limited to 200 characters.");

        var roster = (await repositories.StaffAttendance.RosterAsync(campusId, request.Date, request.Date, ct)).Select(s => s.StaffId).ToHashSet();
        if (entries.Any(e => !roster.Contains(e.StaffId)))
            return Error.Validation($"Someone in the list is not on this campus's staff on {CalendarService.FormatDate(request.Date)}.");

        var existing = (await repositories.StaffAttendance.OnDateTrackedAsync(campusId, request.Date, ct)).ToDictionary(a => a.StaffId);
        foreach (var e in entries)
        {
            if (!existing.TryGetValue(e.StaffId, out var row))
            {
                row = new StaffAttendance { CampusId = campusId, StaffId = e.StaffId, Date = request.Date };
                repositories.StaffAttendance.Add(row);
            }

            row.Status = e.Status;
            row.CheckIn = e.CheckIn;
            row.Note = string.IsNullOrWhiteSpace(e.Note) ? null : e.Note.Trim();
            row.MarkedByUserId = currentUser.UserId;
        }

        await repositories.SaveChangesAsync(ct);
        return await GetAsync(request.Date, ct);
    }

    public async Task<Result<StaffMonthlyDto>> MonthlyAsync(string month, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from))
            return Error.Validation("Month must look like 2026-11.");
        var to = from.AddMonths(1).AddDays(-1);

        var kinds = await calendar.DayKindsAsync(campusId, from, to, ct);
        var rows = await repositories.StaffAttendance.BetweenAsync(campusId, from, to, ct);
        var marked = rows.Select(r => r.Date).ToHashSet();
        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i))
            .Select(d => new MonthDayDto(d,
                kinds[d].Kind == DayKind.WeeklyOff && marked.Contains(d) ? DayKind.Working : kinds[d].Kind, // marked before the weekly off changed
                kinds[d].HolidayName, marked.Contains(d))).ToList();
        var working = days.Where(d => d.Kind == DayKind.Working).Select(d => d.Date).ToHashSet();
        var byStaff = rows.GroupBy(r => r.StaffId).ToDictionary(g => g.Key, g => g.ToDictionary(r => r.Date));

        var staff = await repositories.StaffAttendance.RosterAsync(campusId, from, to, ct);
        var result = staff.Select(s =>
        {
            byStaff.TryGetValue(s.StaffId, out var mine);
            mine ??= [];
            var cells = days.Select(d => mine.TryGetValue(d.Date, out var r) ? Code(r.Status) : "").ToList();
            var counted = mine.Values.Where(r => working.Contains(r.Date)).ToList();
            int P = counted.Count(r => r.Status == StaffAttendanceStatus.Present), A = counted.Count(r => r.Status == StaffAttendanceStatus.Absent),
                L = counted.Count(r => r.Status == StaffAttendanceStatus.Late), Lv = counted.Count(r => r.Status == StaffAttendanceStatus.Leave),
                H = counted.Count(r => r.Status == StaffAttendanceStatus.HalfDay);
            return new StaffMonthRowDto(s.StaffId, s.EmployeeNo, s.Name, cells, P, A, L, Lv, H, AttendanceMath.StaffPct(P, L, H, counted.Count, Lv));
        }).ToList();
        return new StaffMonthlyDto(from.ToString("yyyy-MM"), days, result);
    }

    private static string Code(StaffAttendanceStatus s) => s switch
    {
        StaffAttendanceStatus.Present => "P",
        StaffAttendanceStatus.Absent => "A",
        StaffAttendanceStatus.Late => "L",
        StaffAttendanceStatus.Leave => "Lv",
        _ => "H"
    };
}
