using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Api.Controllers;

public class SubjectsController(ISubjectService subjectService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Subjects, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<SubjectDto>>>> List(CancellationToken ct) =>
        FromResult(await subjectService.ListAsync(ct));

    [HttpPost]
    [HasPermission(AppModules.Subjects, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<SubjectDto>>> Create([FromBody] SaveSubjectRequest request, CancellationToken ct) =>
        FromResult(await subjectService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Subjects, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<SubjectDto>>> Update(int id, [FromBody] SaveSubjectRequest request, CancellationToken ct) =>
        FromResult(await subjectService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Subjects, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await subjectService.DeleteAsync(id, ct));
}
