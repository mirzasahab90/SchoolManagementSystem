using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Dashboard;

namespace SchoolManagementSystem.Api.Controllers;

public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Dashboard, AppActions.View)]
    public async Task<ActionResult<ResponseDto<DashboardDto>>> Get(CancellationToken ct) =>
        FromResult(await dashboardService.GetAsync(ct));
}
