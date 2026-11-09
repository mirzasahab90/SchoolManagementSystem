using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Api.Controllers;

public class SectionsController(ISectionService sectionService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Sections, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<SectionDto>>>> List([FromQuery] int? sessionId, [FromQuery] int? classId, CancellationToken ct) =>
        FromResult(await sectionService.ListAsync(sessionId, classId, ct));

    [HttpPost]
    [HasPermission(AppModules.Sections, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<SectionDto>>> Create([FromBody] SaveSectionRequest request, CancellationToken ct) =>
        FromResult(await sectionService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Sections, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<SectionDto>>> Update(int id, [FromBody] SaveSectionRequest request, CancellationToken ct) =>
        FromResult(await sectionService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Sections, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await sectionService.DeleteAsync(id, ct));

    [HttpPost("copy")]
    [HasPermission(AppModules.Sections, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<CopySectionsResultDto>>> Copy([FromBody] CopySectionsRequest request, CancellationToken ct) =>
        FromResult(await sectionService.CopyAsync(request, ct));
}
