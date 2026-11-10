using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Users;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserListItemDto>>> ListAsync(CancellationToken ct = default);

    Task<Result<UserListItemDto>> GetAsync(int id, CancellationToken ct = default);

    Task<Result<UserListItemDto>> CreateAsync(SaveUserRequest request, CancellationToken ct = default);

    /// <summary>Null when the current user may change this user on the Users page (same rules as edit / delete); else the error.</summary>
    Task<Error?> CheckCanManageAsync(int userId, CancellationToken ct = default);

    Task<Result<UserListItemDto>> UpdateAsync(int id, SaveUserRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
