using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Org;
using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Application.Services.Roles;

public class RoleService(SchoolManagementSystemRepositories repositories, IPermissionChecker permissionChecker) : IRoleService
{
    public async Task<Result<IReadOnlyList<RoleListItemDto>>> ListAsync(CancellationToken ct = default) =>
        await repositories.Roles.ListAsync(ct);

    public async Task<Result<RoleDetailDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var role = await repositories.Roles.GetTrackedAsync(id, ct);
        if (role == null) return Error.NotFound("Role");
        return await ToDetailAsync(role, ct);
    }

    public async Task<Result<RoleDetailDto>> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("Role name is required.");

        var name = request.Name.Trim();
        if (await repositories.Roles.NameExistsAsync(name, null, ct))
            return Error.Conflict($"Role '{name}' already exists.");

        var role = new Role
        {
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = true,
            IsSystem = false
        };
        repositories.Roles.Add(role);
        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return await GetAsync(role.Id, ct);
    }

    public async Task<Result<RoleDetailDto>> UpdateAsync(int id, SaveRolePermissionsRequest request, CancellationToken ct = default)
    {
        var role = await repositories.Roles.GetTrackedAsync(id, ct);
        if (role == null) return Error.NotFound("Role");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("Role name is required.");

        var name = request.Name.Trim();
        if (await repositories.Roles.NameExistsAsync(name, id, ct))
            return Error.Conflict($"Role '{name}' already exists.");

        if (role.IsSystem && !string.Equals(role.Name, name, StringComparison.Ordinal))
            return Error.Validation("System role name cannot be changed.");

        if (role.Name == SystemRoles.SuperAdmin)
        {
            role.Description = request.Description?.Trim();
            await repositories.SaveChangesAsync(ct);
            permissionChecker.Invalidate();
            return await GetAsync(id, ct);
        }

        role.Name = name;
        role.Description = request.Description?.Trim();
        role.IsActive = request.IsActive;

        var modules = await repositories.Roles.GetMenuModulesAsync(ct);
        var permissions = await repositories.Roles.GetPermissionsAsync(ct);
        var permByCode = permissions.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);
        var moduleByKey = modules.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);

        role.Permissions.Clear();
        foreach (var cell in request.Matrix)
        {
            if (!moduleByKey.TryGetValue(cell.ModuleKey, out var module))
                continue;

            void Grant(string action, bool enabled)
            {
                if (!enabled || !permByCode.TryGetValue(action, out var permission)) return;
                role.Permissions.Add(new RoleModulePermission
                {
                    RoleId = role.Id,
                    ModuleId = module.Id,
                    PermissionId = permission.Id
                });
            }

            Grant(AppActions.View, cell.View);
            Grant(AppActions.Add, cell.Add);
            Grant(AppActions.Edit, cell.Edit);
            Grant(AppActions.Delete, cell.Delete);
        }

        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return await GetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var role = await repositories.Roles.GetTrackedAsync(id, ct);
        if (role == null) return Error.NotFound("Role");
        if (role.IsSystem) return Error.Validation("System roles cannot be deleted.");

        var users = await repositories.Roles.CountUsersAsync(id, ct);
        if (users > 0)
            return Error.Validation($"Role is assigned to {users} user(s). Reassign them first.");

        repositories.Roles.SoftDelete(role);
        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return Result.Ok("Role deleted.");
    }

    private async Task<RoleDetailDto> ToDetailAsync(Role role, CancellationToken ct)
    {
        var modules = await repositories.Roles.GetMenuModulesAsync(ct);
        var permissions = await repositories.Roles.GetPermissionsAsync(ct);
        var byId = permissions.ToDictionary(p => p.Id);

        var granted = role.Permissions
            .GroupBy(p => p.ModuleId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => byId.TryGetValue(x.PermissionId, out var p) ? p.Code : "")
                    .Where(c => c.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));

        var matrix = modules.Select(m =>
        {
            granted.TryGetValue(m.Id, out var actions);
            actions ??= [];
            return new ModulePermissionCellDto(
                m.Key,
                m.Name,
                actions.Contains(AppActions.View),
                actions.Contains(AppActions.Add),
                actions.Contains(AppActions.Edit),
                actions.Contains(AppActions.Delete));
        }).ToList();

        return new RoleDetailDto(role.Id, role.Name, role.Description, role.IsSystem, role.IsActive, matrix);
    }
}
