using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Auth;

namespace SchoolManagementSystem.Api.Controllers;

public class AuthController(
    IAuthService authService,
    RefreshCookie refreshCookie,
    ICurrentUser currentUser) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ResponseDto<AuthResult>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        if (!result.IsSuccess)
        {
            return Failure<AuthResult>(result.Error!);
        }

        refreshCookie.Set(Response, result.Value!.Tokens.RefreshToken, result.Value.Tokens.RefreshExpiresUtc);
        return Success(result.Value);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<ResponseDto<AuthResult>>> Refresh(CancellationToken ct)
    {
        var token = refreshCookie.Read(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new ResponseDto<AuthResult>(null, 401, "Session expired. Please sign in again."));
        }

        var result = await authService.RefreshAsync(token, ct);
        if (result == null)
        {
            refreshCookie.Clear(Response);
            return Unauthorized(new ResponseDto<AuthResult>(null, 401, "Session expired. Please sign in again."));
        }

        refreshCookie.Set(Response, result.Tokens.RefreshToken, result.Tokens.RefreshExpiresUtc);
        return Success(result);
    }

    [HttpPost("logout")]
    public async Task<ActionResult<ResponseDto<object>>> Logout(CancellationToken ct)
    {
        await authService.LogoutAsync(refreshCookie.Read(Request), ct);
        refreshCookie.Clear(Response);
        return Success<object>(null!, "Signed out.");
    }

    [HttpPost("change-password")]
    public async Task<ActionResult<ResponseDto<object>>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct) =>
        FromResult(await authService.ChangePasswordAsync(CurrentUserId, request, ct));

    [HttpGet("me")]
    public async Task<ActionResult<ResponseDto<CurrentUserDto>>> Me(CancellationToken ct)
    {
        var result = await authService.GetCurrentUserAsync(CurrentUserId, currentUser.ActiveCampusId, ct);
        return FromResult(result);
    }
}
