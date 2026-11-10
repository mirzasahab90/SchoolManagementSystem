using SchoolManagementSystem.Domain.DTOs.Exams;
using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Attendance;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Api.Controllers;

public class StudentsController(
    IStudentService studentService, IStudentFileService fileService, IAttendanceReportService attendance, IReportCardService cards) : ApiControllerBase
{
    [HttpGet("{id:int}/results")]
    [HasAnyPermission("students:View", "results:View")]
    public async Task<ActionResult<ResponseDto<StudentResultsDto>>> Results(int id, CancellationToken ct) =>
        FromResult(await cards.StudentResultsAsync(id, ct));

    [HttpGet("{id:int}/attendance")]
    [HasAnyPermission("students:View", "attendance-reports:View")]
    public async Task<ActionResult<ResponseDto<StudentAttendanceDto>>> Attendance(int id, CancellationToken ct) =>
        FromResult(await attendance.StudentAsync(id, ct));

    [HttpGet]
    [HasPermission(AppModules.Students, AppActions.View)]
    public async Task<ActionResult<ResponseDto<PagedResult<StudentListItemDto>>>> List(
        [FromQuery] string? q, [FromQuery] int? classId, [FromQuery] int? sectionId, [FromQuery] StudentStatus? status,
        [FromQuery] int? sessionId, [FromQuery] DateOnly? admittedFrom, [FromQuery] DateOnly? admittedTo,
        [FromQuery] string? sort, [FromQuery] string? dir,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        FromResult(await studentService.SearchAsync(q, classId, sectionId, status, sessionId, admittedFrom, admittedTo, sort, dir,
            page, Math.Clamp(pageSize, 1, 100), ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Students, AppActions.View)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await studentService.GetProfileAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Students, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> Admit([FromBody] AdmissionRequest request, CancellationToken ct) =>
        FromResult(await studentService.AdmitAsync(request, ct));

    [HttpPost("{id:int}/guardians")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> AddGuardian(int id, [FromBody] GuardianLinkRequest request, CancellationToken ct) =>
        FromResult(await studentService.AddGuardianAsync(id, request, ct));

    [HttpPatch("{id:int}/guardians/{guardianId:int}")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> UpdateGuardianLink(
        int id, int guardianId, [FromBody] UpdateGuardianLinkRequest request, CancellationToken ct) =>
        FromResult(await studentService.UpdateGuardianLinkAsync(id, guardianId, request, ct));

    [HttpDelete("{id:int}/guardians/{guardianId:int}")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> RemoveGuardian(int id, int guardianId, CancellationToken ct) =>
        FromResult(await studentService.RemoveGuardianAsync(id, guardianId, ct));

    [HttpPost("{id:int}/change-section")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> ChangeSection(int id, [FromBody] ChangeSectionRequest request, CancellationToken ct) =>
        FromResult(await studentService.ChangeSectionAsync(id, request, ct));

    [HttpPost("{id:int}/leave")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> Leave(int id, [FromBody] LeaveRequest request, CancellationToken ct) =>
        FromResult(await studentService.LeaveAsync(id, request, ct));

    [HttpPost("{id:int}/rejoin")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> Rejoin(int id, [FromBody] RejoinRequest request, CancellationToken ct) =>
        FromResult(await studentService.RejoinAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Students, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await studentService.DeleteAsync(id, ct));

    [HttpPost("{id:int}/photo")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    [RequestSizeLimit(11_000_000)]
    public async Task<ActionResult<ResponseDto<object>>> SetPhoto(int id, IFormFile file, CancellationToken ct) =>
        FromResult(await fileService.SetPhotoAsync(id, file, ct));

    [HttpGet("{id:int}/photo")]
    [HasPermission(AppModules.Students, AppActions.View)]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken ct)
    {
        var result = await fileService.GetPhotoAsync(id, ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType) : Failure<object>(result.Error!);
    }

    [HttpPost("{id:int}/documents")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    [RequestSizeLimit(11_000_000)]
    public async Task<ActionResult<ResponseDto<StudentDocumentDto>>> UploadDocument(
        int id, [FromForm] DocumentType type, IFormFile file, CancellationToken ct) =>
        FromResult(await fileService.UploadDocumentAsync(id, type, file, ct));

    [HttpGet("{id:int}/documents/{docId:int}")]
    [HasPermission(AppModules.Students, AppActions.View)]
    public async Task<IActionResult> GetDocument(int id, int docId, CancellationToken ct)
    {
        var result = await fileService.GetDocumentAsync(id, docId, ct);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Failure<object>(result.Error!);
    }

    [HttpDelete("{id:int}/documents/{docId:int}")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<object>>> DeleteDocument(int id, int docId, CancellationToken ct) =>
        FromResult(await fileService.DeleteDocumentAsync(id, docId, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Students, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StudentProfileDto>>> Update(int id, [FromBody] SaveStudentRequest request, CancellationToken ct) =>
        FromResult(await studentService.UpdateAsync(id, request, ct));
}
