using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Users;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IUserService
{
    Task<Result<IReadOnlyList<UserListItemDto>>> ListAsync(CancellationToken ct = default);

    Task<Result<UserListItemDto>> GetAsync(int id, CancellationToken ct = default);

    Task<Result<UserListItemDto>> CreateAsync(SaveUserRequest request, CancellationToken ct = default);

    Task<Result<UserListItemDto>> UpdateAsync(int id, SaveUserRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
