using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Users;

namespace SchoolManagementSystem.Api.Controllers;

public class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Users, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<UserListItemDto>>>> List(CancellationToken ct) =>
        FromResult(await userService.ListAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Users, AppActions.View)]
    public async Task<ActionResult<ResponseDto<UserListItemDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await userService.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Users, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<UserListItemDto>>> Create([FromBody] SaveUserRequest request, CancellationToken ct) =>
        FromResult(await userService.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Users, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<UserListItemDto>>> Update(int id, [FromBody] SaveUserRequest request, CancellationToken ct) =>
        FromResult(await userService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Users, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await userService.DeleteAsync(id, ct));
}
