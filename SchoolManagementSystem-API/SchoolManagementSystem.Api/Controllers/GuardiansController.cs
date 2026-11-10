using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Students;

namespace SchoolManagementSystem.Api.Controllers;

public class GuardiansController(IGuardianService guardianService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Guardians, AppActions.View)]
    public async Task<ActionResult<ResponseDto<PagedResult<GuardianListItemDto>>>> List(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        FromResult(await guardianService.SearchAsync(q, page, pageSize, ct));

    /// <summary>Used by the admission form, so it follows the students Add permission.</summary>
    [HttpGet("lookup")]
    [HasPermission(AppModules.Students, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<GuardianLookupDto>>>> Lookup(
        [FromQuery] string? cnic, [FromQuery] string? phone, CancellationToken ct) =>
        FromResult(await guardianService.LookupAsync(cnic, phone, ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Guardians, AppActions.View)]
    public async Task<ActionResult<ResponseDto<GuardianDetailDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await guardianService.GetAsync(id, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Guardians, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<GuardianDetailDto>>> Update(int id, [FromBody] SaveGuardianRequest request, CancellationToken ct) =>
        FromResult(await guardianService.UpdateAsync(id, request, ct));
}
