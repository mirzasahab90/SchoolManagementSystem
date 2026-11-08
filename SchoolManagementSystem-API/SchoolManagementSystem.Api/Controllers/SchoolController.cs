using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Org;

namespace SchoolManagementSystem.Api.Controllers;

public class SchoolController(ISchoolService schoolService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Settings, AppActions.View)]
    public async Task<ActionResult<ResponseDto<SchoolDto>>> Get(CancellationToken ct) =>
        FromResult(await schoolService.GetAsync(ct));

    [HttpPut]
    [HasPermission(AppModules.Settings, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<SchoolDto>>> Update([FromBody] SaveSchoolRequest request, CancellationToken ct) =>
        FromResult(await schoolService.UpdateAsync(request, ct));
}
