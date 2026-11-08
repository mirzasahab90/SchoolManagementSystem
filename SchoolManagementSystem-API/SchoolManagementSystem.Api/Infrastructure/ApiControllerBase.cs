using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs;

namespace SchoolManagementSystem.Api.Infrastructure;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var userId) ? userId : 0;
        }
    }

    protected ActionResult<ResponseDto<T>> Success<T>(T data, string? message = null) =>
        Ok(new ResponseDto<T>(data, StatusCodes.Status200OK, message));

    protected ActionResult<ResponseDto<T>> FromResult<T>(Result<T> result) =>
        result.IsSuccess ? Success(result.Value!) : Failure<T>(result.Error!);

    protected ActionResult<ResponseDto<object>> FromResult(Result result) =>
        result.IsSuccess ? Success<object>(null!, result.Message ?? "Done.") : Failure<object>(result.Error!);

    protected ObjectResult Failure<T>(Error error) =>
        StatusCode(error.Status, new ResponseDto<T>(default, error.Status, error.Message));
}
