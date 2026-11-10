using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Services.Hiring;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Users;

namespace SchoolManagementSystem.Api.Controllers;

public class UsersController(IUserService userService, IUserPicker picker) : ApiControllerBase
{
    /// <summary>The users list (users:View), or with <c>?pick=true</c> id and name of the working campus's active users (also for candidates:Edit).</summary>
    [HttpGet]
    [HasAnyPermission("users:View", "candidates:Edit")]
    public async Task<IActionResult> List([FromQuery] bool pick = false, [FromQuery] int? candidateId = null, CancellationToken ct = default)
    {
        if (pick)
        {
            var picks = await picker.PickAsync(candidateId, ct);
            return picks.IsSuccess ? Success(picks.Value!).Result! : Failure<object>(picks.Error!);
        }

        if (!await picker.CanAsync(AppModules.Users, AppActions.View, ct)) return Failure<object>(Domain.Common.Error.Forbidden());
        var list = await userService.ListAsync(ct);
        return list.IsSuccess ? Success(list.Value!).Result! : Failure<object>(list.Error!);
    }

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
