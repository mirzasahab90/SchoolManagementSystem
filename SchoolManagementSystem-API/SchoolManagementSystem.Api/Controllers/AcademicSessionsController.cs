using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Api.Controllers;

public class AcademicSessionsController(IAcademicSessionService sessionService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.AcademicSessions, AppActions.View)]
    public async Task<ActionResult<ResponseDto<AcademicSessionListDto>>> List(CancellationToken ct) =>
        FromResult(await sessionService.ListAsync(ct));

    [HttpPost]
    [HasPermission(AppModules.AcademicSessions, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<AcademicSessionDto>>> Create([FromBody] SaveAcademicSessionRequest request, CancellationToken ct) =>
        FromResult(await sessionService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.AcademicSessions, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<AcademicSessionDto>>> Update(int id, [FromBody] SaveAcademicSessionRequest request, CancellationToken ct) =>
        FromResult(await sessionService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.AcademicSessions, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await sessionService.DeleteAsync(id, ct));

    [HttpPost("{id:int}/set-current")]
    [HasPermission(AppModules.AcademicSessions, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<object>>> SetCurrent(int id, CancellationToken ct) =>
        FromResult(await sessionService.SetCurrentAsync(id, ct));
}
