using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Attendance;

namespace SchoolManagementSystem.Api.Controllers;

public class AttendanceController(IAttendanceService service, IAttendanceReportService reports) : ApiControllerBase
{
    [HttpGet("today")]
    [HasPermission(AppModules.AttendanceReports, AppActions.View)]
    public async Task<ActionResult<ResponseDto<TodayDto>>> Today(CancellationToken ct) =>
        FromResult(await reports.TodayAsync(ct));

    [HttpGet("monthly")]
    [HasPermission(AppModules.AttendanceReports, AppActions.View)]
    public async Task<ActionResult<ResponseDto<MonthlyDto>>> Monthly([FromQuery] int sectionId, [FromQuery] string month, CancellationToken ct) =>
        FromResult(await reports.MonthlyAsync(sectionId, month, ct));

    [HttpGet("monthly.csv")]
    [HasPermission(AppModules.AttendanceReports, AppActions.View)]
    public async Task<IActionResult> MonthlyCsv([FromQuery] int sectionId, [FromQuery] string month, CancellationToken ct)
    {
        var result = await reports.MonthlyCsvAsync(sectionId, month, ct);
        return result.IsSuccess ? File(result.Value.Content, "text/csv; charset=utf-8", result.Value.FileName) : Failure<object>(result.Error!);
    }

    [HttpGet("low")]
    [HasPermission(AppModules.AttendanceReports, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<LowDto>>>> Low([FromQuery] decimal threshold = 75, [FromQuery] int? sectionId = null, CancellationToken ct = default) =>
        FromResult(await reports.LowAsync(threshold, sectionId, ct));

    [HttpGet("my-sections")]
    [HasPermission(AppModules.Attendance, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<MySectionDto>>>> MySections(CancellationToken ct) =>
        FromResult(await service.MySectionsAsync(ct));

    [HttpGet("sheet")]
    [HasPermission(AppModules.Attendance, AppActions.View)]
    public async Task<ActionResult<ResponseDto<SheetDto>>> Sheet([FromQuery] int sectionId, [FromQuery] DateOnly date, CancellationToken ct) =>
        FromResult(await service.GetSheetAsync(sectionId, date, ct));

    /// <summary>Add is the gate here; who may mark which section and date is checked by AttendanceAccess.</summary>
    [HttpPut("sheet")]
    [HasPermission(AppModules.Attendance, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<SheetDto>>> Save([FromBody] SaveSheetRequest request, CancellationToken ct) =>
        FromResult(await service.SaveSheetAsync(request, ct));
}

public class StaffAttendanceController(IStaffAttendanceService service) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.StaffAttendance, AppActions.View)]
    public async Task<ActionResult<ResponseDto<StaffSheetDto>>> Get([FromQuery] DateOnly date, CancellationToken ct) =>
        FromResult(await service.GetAsync(date, ct));

    [HttpPut]
    [HasPermission(AppModules.StaffAttendance, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<StaffSheetDto>>> Save([FromBody] SaveStaffSheetRequest request, CancellationToken ct) =>
        FromResult(await service.SaveAsync(request, ct));

    [HttpGet("monthly")]
    [HasPermission(AppModules.StaffAttendance, AppActions.View)]
    public async Task<ActionResult<ResponseDto<StaffMonthlyDto>>> Monthly([FromQuery] string month, CancellationToken ct) =>
        FromResult(await service.MonthlyAsync(month, ct));
}

public class CalendarController(ICalendarService service) : ApiControllerBase
{
    [HttpGet]
    [HasAnyPermission("calendar:View", "attendance:View", "staff-attendance:View")]
    public async Task<ActionResult<ResponseDto<CalendarDto>>> Get(CancellationToken ct) =>
        FromResult(await service.GetAsync(ct));

    [HttpPut("weekly-off")]
    [HasPermission(AppModules.Calendar, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CalendarDto>>> WeeklyOff([FromBody] WeeklyOffRequest request, CancellationToken ct) =>
        FromResult(await service.SetWeeklyOffAsync(request, ct));

    [HttpPost("holidays")]
    [HasPermission(AppModules.Calendar, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<HolidayDto>>> CreateHoliday([FromBody] SaveHolidayRequest request, CancellationToken ct) =>
        FromResult(await service.CreateHolidayAsync(request, ct));

    [HttpPut("holidays/{id:int}")]
    [HasPermission(AppModules.Calendar, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<HolidayDto>>> UpdateHoliday(int id, [FromBody] SaveHolidayRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateHolidayAsync(id, request, ct));

    [HttpDelete("holidays/{id:int}")]
    [HasPermission(AppModules.Calendar, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> DeleteHoliday(int id, CancellationToken ct) =>
        FromResult(await service.DeleteHolidayAsync(id, ct));
}
