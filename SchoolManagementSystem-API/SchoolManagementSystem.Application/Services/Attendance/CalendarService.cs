using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Application.Services.Academics;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Attendance;
using SchoolManagementSystem.Domain.Entities.Attendance;

namespace SchoolManagementSystem.Application.Services.Attendance;

/// <summary>Weekly offs and holidays, and the one answer every attendance rule asks: is this a working day?</summary>
public class CalendarService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : ICalendarService
{
    public const int MaxHolidayDays = 60;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static string FormatDate(DateOnly d) => d.ToString("d MMM yyyy");

    internal static IReadOnlyList<DayOfWeek> ParseWeeklyOff(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Enum.TryParse<DayOfWeek>(x.Trim(), out var d) ? (DayOfWeek?)d : null)
                .Where(d => d != null).Select(d => d!.Value).OrderBy(d => ((int)d + 6) % 7).ToList();

    public async Task<Result<CalendarDto>> GetAsync(CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var weeklyOff = ParseWeeklyOff(await repositories.Calendar.WeeklyOffAsync(campusId, ct));
        var holidays = (await repositories.Calendar.ListForCampusAsync(campusId, ct)).Select(ToDto).ToList();
        return new CalendarDto(weeklyOff, holidays);
    }

    public async Task<Result<CalendarDto>> SetWeeklyOffAsync(WeeklyOffRequest request, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
        var days = (request.Days ?? []).Distinct().ToList();
        if (days.Any(d => !Enum.IsDefined(d))) return Error.Validation("Unknown day.");
        if (days.Count > 6) return Error.Validation("At least one day of the week must be a working day.");

        var calendar = await repositories.Calendar.GetTrackedAsync(campusId, ct);
        if (calendar == null)
        {
            calendar = new CampusCalendar { CampusId = campusId };
            repositories.Calendar.Add(calendar);
        }

        calendar.WeeklyOff = string.Join(',', days.OrderBy(d => ((int)d + 6) % 7));
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<Result<HolidayDto>> CreateHolidayAsync(SaveHolidayRequest request, CancellationToken ct = default)
    {
        var error = Validate(request);
        if (error != null) return error;

        int? owner;
        if (request.AllCampuses)
        {
            if (!currentUser.IsHq) return Error.Forbidden("Only Main Campus can add a holiday for all campuses.");
            owner = null;
        }
        else
        {
            if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
            owner = campusId;
        }

        var holiday = new Holiday { CampusId = owner };
        Apply(holiday, request);
        repositories.Calendar.Add(holiday);
        await repositories.SaveChangesAsync(ct);
        return ToDto((await repositories.Calendar.GetHolidayAsync(holiday.Id, ct))!);
    }

    public async Task<Result<HolidayDto>> UpdateHolidayAsync(int id, SaveHolidayRequest request, CancellationToken ct = default)
    {
        var holiday = await repositories.Calendar.GetHolidayAsync(id, ct);
        if (holiday == null) return Error.NotFound("Holiday");
        var writeError = Ownership.CheckWrite(currentUser, holiday.CampusId, "Holiday");
        if (writeError != null) return writeError;
        var error = Validate(request);
        if (error != null) return error;

        Apply(holiday, request);
        await repositories.SaveChangesAsync(ct);
        return ToDto(holiday);
    }

    public async Task<Result> DeleteHolidayAsync(int id, CancellationToken ct = default)
    {
        var holiday = await repositories.Calendar.GetHolidayAsync(id, ct);
        if (holiday == null) return Error.NotFound("Holiday");
        var writeError = Ownership.CheckWrite(currentUser, holiday.CampusId, "Holiday");
        if (writeError != null) return writeError;
        repositories.Calendar.Remove(holiday);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Holiday deleted.");
    }

    /// <summary>The kind of day for a campus (sessions are checked by the callers).</summary>
    public async Task<DayInfo> IsWorkingDayAsync(int campusId, DateOnly date, CancellationToken ct = default)
    {
        if (date > Today) return new DayInfo(DayKind.Future, "You cannot mark a future date.");
        var off = ParseWeeklyOff(await repositories.Calendar.WeeklyOffAsync(campusId, ct));
        if (off.Contains(date.DayOfWeek)) return new DayInfo(DayKind.WeeklyOff, $"{date.DayOfWeek} is a weekly off.");
        var holiday = (await repositories.Calendar.HolidaysAsync(campusId, date, date, ct)).FirstOrDefault();
        if (holiday != null) return new DayInfo(DayKind.Holiday, $"{FormatDate(date)} is a holiday: {holiday.Name}.");
        return new DayInfo(DayKind.Working, null);
    }

    /// <summary>Kinds of every date in [from, to] for a campus, with holiday names (no session or future check).</summary>
    public async Task<Dictionary<DateOnly, (DayKind Kind, string? HolidayName)>> DayKindsAsync(
        int campusId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var off = ParseWeeklyOff(await repositories.Calendar.WeeklyOffAsync(campusId, ct));
        var holidays = await repositories.Calendar.HolidaysAsync(campusId, from, to, ct);
        var result = new Dictionary<DateOnly, (DayKind, string?)>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var h = holidays.FirstOrDefault(x => x.StartDate <= d && x.EndDate >= d);
            result[d] = h != null ? (DayKind.Holiday, h.Name)
                : off.Contains(d.DayOfWeek) ? (DayKind.WeeklyOff, null)
                : d > Today ? (DayKind.Future, null)
                : (DayKind.Working, null);
        }

        return result;
    }

    private static Error? Validate(SaveHolidayRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return Error.Validation("Name is required.");
        if (r.Name.Trim().Length > 100) return Error.Validation("Name is too long (100 characters at most).");
        if (r.EndDate < r.StartDate) return Error.Validation("The end date cannot be before the start date.");
        if (r.EndDate.DayNumber - r.StartDate.DayNumber + 1 > MaxHolidayDays)
            return Error.Validation($"A holiday can be at most {MaxHolidayDays} days long.");
        return null;
    }

    private static void Apply(Holiday h, SaveHolidayRequest r)
    {
        h.Name = r.Name.Trim();
        h.StartDate = r.StartDate;
        h.EndDate = r.EndDate;
    }

    private static HolidayDto ToDto(Holiday h) => new(h.Id, h.CampusId, h.Campus?.Code, h.Name, h.StartDate, h.EndDate);
}
