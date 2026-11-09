using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Org;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IRoleService
{
    Task<Result<IReadOnlyList<RoleListItemDto>>> ListAsync(bool assignableOnly = false, CancellationToken ct = default);

    Task<Result<RoleDetailDto>> GetAsync(int id, CancellationToken ct = default);

    Task<Result<RoleDetailDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);

    Task<Result<RoleDetailDto>> UpdateAsync(int id, SaveRolePermissionsRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
