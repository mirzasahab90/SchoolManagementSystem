using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Auth;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResult>> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);

    Task<Result<CurrentUserDto>> GetCurrentUserAsync(int userId, int? activeCampusId, CancellationToken ct = default);
}
