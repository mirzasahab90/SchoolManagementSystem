using SchoolManagementSystem.Domain.Entities.Attendance;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Domain.DTOs.Attendance;

// ---------------------------------------------------------------- calendar

public record HolidayDto(int Id, int? CampusId, string? CampusCode, string Name, DateOnly StartDate, DateOnly EndDate);

public record CalendarDto(IReadOnlyList<DayOfWeek> WeeklyOff, IReadOnlyList<HolidayDto> Holidays);

public record WeeklyOffRequest(IReadOnlyList<DayOfWeek> Days);

public record SaveHolidayRequest(string Name, DateOnly StartDate, DateOnly EndDate, bool AllCampuses);

/// <summary>Whether a campus works on a date, and why not.</summary>
public record DayInfo(DayKind Kind, string? Reason)
{
    public bool IsWorking => Kind == DayKind.Working;
}

// ---------------------------------------------------------------- student marking

public record SheetRowDto(int StudentId, int RollNo, string Name, bool HasPhoto, StudentAttendanceStatus Status, string? Note);

public record SheetDto(
    int SectionId, string ClassName, string SectionName, DateOnly Date, bool IsWorkingDay, string? Reason, bool CanEdit,
    string? EditLockedReason, bool Marked, string? MarkedBy, DateTime? MarkedOn, IReadOnlyList<SheetRowDto> Rows);

public record SheetEntryRequest(int StudentId, StudentAttendanceStatus Status, string? Note);

public record SaveSheetRequest(int SectionId, DateOnly Date, IReadOnlyList<SheetEntryRequest> Entries);

public record MySectionDto(int SectionId, string ClassName, string SectionName, bool IsClassTeacher, bool MarkedToday);

// ---------------------------------------------------------------- reports

public record TodaySectionDto(
    int SectionId, string ClassName, string SectionName, string? ClassTeacherName, bool Marked, int Present, int Absent, int Late, int Leave);

public record AbsenteeDto(int StudentId, string Name, string ClassName, string SectionName, StudentAttendanceStatus Status);

public record TodayDto(
    DateOnly Date, bool IsWorkingDay, string? Reason, decimal? PresentPct, IReadOnlyList<TodaySectionDto> Sections,
    IReadOnlyList<AbsenteeDto> Absentees);

public record MonthDayDto(DateOnly Date, DayKind Kind, string? HolidayName, bool Marked);

public record MonthStudentDto(
    int StudentId, int RollNo, string Name, IReadOnlyList<string> Cells, int Present, int Absent, int Late, int Leave, decimal? Pct);

public record MonthlyDto(int SectionId, string ClassName, string SectionName, string Month, IReadOnlyList<MonthDayDto> Days, IReadOnlyList<MonthStudentDto> Students);

public record LowDto(int StudentId, string Name, string ClassName, string SectionName, int WorkingDays, int Attended, decimal Pct);

public record MonthSummaryDto(string Month, int Present, int Absent, int Late, int Leave, decimal? Pct);

public record AbsenceDto(DateOnly Date, StudentAttendanceStatus Status, string? Note);

public record StudentAttendanceDto(
    decimal? Pct, int Present, int Absent, int Late, int Leave, int WorkingDaysMarked, IReadOnlyList<MonthSummaryDto> Months,
    IReadOnlyList<AbsenceDto> Absences);

// ---------------------------------------------------------------- staff

public record StaffSheetRowDto(
    int StaffId, string EmployeeNo, string Name, StaffDesignation Designation, StaffAttendanceStatus? Status, TimeOnly? CheckIn,
    string? Note, bool Marked);

public record StaffSheetDto(DateOnly Date, bool IsWorkingDay, string? Reason, IReadOnlyList<StaffSheetRowDto> Rows);

public record StaffSheetEntryRequest(int StaffId, StaffAttendanceStatus Status, TimeOnly? CheckIn, string? Note);

public record SaveStaffSheetRequest(DateOnly Date, IReadOnlyList<StaffSheetEntryRequest> Entries);

public record StaffMonthRowDto(
    int StaffId, string EmployeeNo, string Name, IReadOnlyList<string> Cells, int Present, int Absent, int Late, int Leave, int HalfDay, decimal? Pct);

public record StaffMonthlyDto(string Month, IReadOnlyList<MonthDayDto> Days, IReadOnlyList<StaffMonthRowDto> Staff);
