using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Org;

namespace SchoolManagementSystem.Api.Controllers;

public class RolesController(IRoleService roleService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Roles, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<RoleListItemDto>>>> List([FromQuery] bool assignable, CancellationToken ct) =>
        FromResult(await roleService.ListAsync(assignable, ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Roles, AppActions.View)]
    public async Task<ActionResult<ResponseDto<RoleDetailDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await roleService.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Roles, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<RoleDetailDto>>> Create([FromBody] CreateRoleRequest request, CancellationToken ct) =>
        FromResult(await roleService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Roles, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<RoleDetailDto>>> Update(int id, [FromBody] SaveRolePermissionsRequest request, CancellationToken ct) =>
        FromResult(await roleService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Roles, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await roleService.DeleteAsync(id, ct));
}
