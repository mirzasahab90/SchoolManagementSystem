using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Auth;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResult>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Changes the signed-in user's password, clears MustChangePassword, signs out every other session and
    /// returns fresh tokens so this device stays signed in.
    /// </summary>
    Task<Result<AuthResult>> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default);

    Task<Result<CurrentUserDto>> GetCurrentUserAsync(int userId, int? activeCampusId, CancellationToken ct = default);
}
