using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Org;

namespace SchoolManagementSystem.Api.Controllers;

public class CampusesController(ICampusService campusService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Campuses, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<CampusDto>>>> List(CancellationToken ct) =>
        FromResult(await campusService.ListAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Campuses, AppActions.View)]
    public async Task<ActionResult<ResponseDto<CampusDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await campusService.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Campuses, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<CampusDto>>> Create([FromBody] SaveCampusRequest request, CancellationToken ct) =>
        FromResult(await campusService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Campuses, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CampusDto>>> Update(int id, [FromBody] SaveCampusRequest request, CancellationToken ct) =>
        FromResult(await campusService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Campuses, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await campusService.DeleteAsync(id, ct));
}
