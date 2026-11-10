using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Attendance;

namespace SchoolManagementSystem.Application.Interfaces;

public interface ICalendarService
{
    /// <summary>Weekly off and holidays of the working campus (plus all-campus holidays).</summary>
    Task<Result<CalendarDto>> GetAsync(CancellationToken ct = default);
    Task<Result<CalendarDto>> SetWeeklyOffAsync(WeeklyOffRequest request, CancellationToken ct = default);
    Task<Result<HolidayDto>> CreateHolidayAsync(SaveHolidayRequest request, CancellationToken ct = default);
    Task<Result<HolidayDto>> UpdateHolidayAsync(int id, SaveHolidayRequest request, CancellationToken ct = default);
    Task<Result> DeleteHolidayAsync(int id, CancellationToken ct = default);
}

public interface IAttendanceService
{
    /// <summary>Sections the caller may mark: all of the campus's current-session sections for admins, else their class-teacher sections.</summary>
    Task<Result<IReadOnlyList<MySectionDto>>> MySectionsAsync(CancellationToken ct = default);

    /// <summary>The roster with saved (or default Present) statuses, plus whether the caller may edit and why not.</summary>
    Task<Result<SheetDto>> GetSheetAsync(int sectionId, DateOnly date, CancellationToken ct = default);

    Task<Result<SheetDto>> SaveSheetAsync(SaveSheetRequest request, CancellationToken ct = default);
}

public interface IAttendanceReportService
{
    Task<Result<TodayDto>> TodayAsync(CancellationToken ct = default);
    Task<Result<MonthlyDto>> MonthlyAsync(int sectionId, string month, CancellationToken ct = default);
    Task<Result<(byte[] Content, string FileName)>> MonthlyCsvAsync(int sectionId, string month, CancellationToken ct = default);

    /// <summary>Current-session students below the threshold (%), optionally for one section.</summary>
    Task<Result<IReadOnlyList<LowDto>>> LowAsync(decimal threshold, int? sectionId, CancellationToken ct = default);

    Task<Result<StudentAttendanceDto>> StudentAsync(int studentId, CancellationToken ct = default);

    /// <summary>A student's attendance % in a session up to a date (no access check; callers check). Null when nothing counted.</summary>
    Task<decimal?> AttendancePctAsync(int studentId, int sessionId, DateOnly to, CancellationToken ct = default);
}

public interface IStaffAttendanceService
{
    /// <summary>Staff working on the date with their saved status (or unmarked).</summary>
    Task<Result<StaffSheetDto>> GetAsync(DateOnly date, CancellationToken ct = default);

    /// <summary>Upserts the given rows; staff not sent are left as they are.</summary>
    Task<Result<StaffSheetDto>> SaveAsync(SaveStaffSheetRequest request, CancellationToken ct = default);

    Task<Result<StaffMonthlyDto>> MonthlyAsync(string month, CancellationToken ct = default);
}
