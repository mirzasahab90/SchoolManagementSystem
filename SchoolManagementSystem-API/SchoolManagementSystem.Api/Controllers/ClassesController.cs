using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Api.Controllers;

public class ClassesController(IClassService classService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Classes, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<ClassDto>>>> List(CancellationToken ct) =>
        FromResult(await classService.ListAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Classes, AppActions.View)]
    public async Task<ActionResult<ResponseDto<ClassDetailDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await classService.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Classes, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<ClassDetailDto>>> Create([FromBody] SaveClassRequest request, CancellationToken ct) =>
        FromResult(await classService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Classes, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<ClassDetailDto>>> Update(int id, [FromBody] SaveClassRequest request, CancellationToken ct) =>
        FromResult(await classService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Classes, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await classService.DeleteAsync(id, ct));

    [HttpPut("{id:int}/subjects")]
    [HasPermission(AppModules.Classes, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<ClassDetailDto>>> SetSubjects(int id, [FromBody] IReadOnlyList<SaveClassSubjectItem> items, CancellationToken ct) =>
        FromResult(await classService.SetSubjectsAsync(id, items, ct));
}
