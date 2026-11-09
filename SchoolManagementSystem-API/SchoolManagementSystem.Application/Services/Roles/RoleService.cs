using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Org;
using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Application.Services.Roles;

/// <summary>
/// HQ roles (no campus) are shared by every campus and only HQ may change them. A campus may keep its own
/// roles, managed by its staff with the Roles permission. Nobody grants a permission they do not hold
/// (HQ excepted), nobody but a SuperAdmin changes a role they hold, and HQ-assign-only flags are never
/// settable through the API.
/// </summary>
public class RoleService(
    SchoolManagementSystemRepositories repositories,
    IPermissionChecker permissionChecker,
    ICurrentUser currentUser) : IRoleService
{
    private static readonly Error HqItem = Error.Forbidden("Only HQ can change head-office items.");

    public async Task<Result<IReadOnlyList<RoleListItemDto>>> ListAsync(bool assignableOnly = false, CancellationToken ct = default)
    {
        var roles = await repositories.Roles.ListAsync(ct);
        if (!assignableOnly) return roles;
        return roles
            .Where(r => r.IsActive)
            .Where(r => !r.HqAssignOnly || currentUser.IsHq)
            .Where(r => r.Name != SystemRoles.SuperAdmin || currentUser.IsSuperAdmin)
            .ToList();
    }

    /// <summary>Null when the current user may change this role.</summary>
    private Error? CheckOwner(Role role) =>
        role.CampusId is int campusId
            ? currentUser.CanAccessCampus(campusId) ? null : Error.NotFound("Role")
            : currentUser.IsHq ? null : HqItem;

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

        // Owner: HQ may pick a campus (or none = HQ role); campus users always create for their working campus.
        int? owner;
        if (currentUser.IsHq)
        {
            owner = request.CampusId;
        }
        else
        {
            if (request.CampusId is int requested && requested != currentUser.ActiveCampusId)
                return Error.Forbidden("You can only create roles for your own campus.");
            owner = currentUser.ActiveCampusId;
            if (owner == null) return Error.Validation("Select a campus.");
        }

        if (owner is int campusId && !currentUser.CanAccessCampus(campusId))
            return Error.Forbidden("You do not have access to this campus.");

        var name = request.Name.Trim();
        if (await repositories.Roles.NameTakenAsync(name, owner, null, ct))
            return Error.Conflict($"Role '{name}' already exists.");

        var role = new Role
        {
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = true,
            IsSystem = false,
            CampusId = owner
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

        var ownerError = CheckOwner(role);
        if (ownerError != null) return ownerError;

        if (role.Name == SystemRoles.SuperAdmin && !currentUser.IsSuperAdmin)
            return Error.Forbidden("Only a SuperAdmin can change the SuperAdmin role.");

        if (!currentUser.IsSuperAdmin && await HoldsRoleAsync(id, ct))
            return Error.Forbidden("You cannot change the permissions of a role you hold.");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Error.Validation("Role name is required.");

        var name = request.Name.Trim();
        if (await repositories.Roles.NameTakenAsync(name, role.CampusId, id, ct))
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

        // No escalation: outside HQ, a role may only be given permissions its editor holds.
        if (!currentUser.IsHq && currentUser.UserId is int actorId)
        {
            var mine = await repositories.Access.GetUserAccessAsync(actorId, ct);
            var exceeds = request.Matrix.Any(cell =>
                (cell.View && !mine.Can(cell.ModuleKey, AppActions.View))
                || (cell.Add && !mine.Can(cell.ModuleKey, AppActions.Add))
                || (cell.Edit && !mine.Can(cell.ModuleKey, AppActions.Edit))
                || (cell.Delete && !mine.Can(cell.ModuleKey, AppActions.Delete)));
            if (exceeds) return Error.Forbidden("You can only grant permissions you have.");
        }

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

        var ownerError = CheckOwner(role);
        if (ownerError != null) return ownerError;

        if (role.IsSystem) return Error.Validation("System roles cannot be deleted.");

        var users = await repositories.Roles.CountUsersAsync(id, ct);
        if (users > 0)
            return Error.Validation($"Role is assigned to {users} user(s). Reassign them first.");

        repositories.Roles.SoftDelete(role);
        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return Result.Ok("Role deleted.");
    }

    private Task<bool> HoldsRoleAsync(int roleId, CancellationToken ct) =>
        currentUser.UserId is int userId
            ? repositories.Roles.UserHasRoleAsync(userId, roleId, ct)
            : Task.FromResult(false);

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

        var campusCode = role.CampusId is int cid ? (await repositories.Campuses.GetDtoAsync(cid, ct))?.Code : null;
        return new RoleDetailDto(role.Id, role.Name, role.Description, role.IsSystem, role.IsActive, matrix,
            role.CampusId, campusCode, role.HqAssignOnly);
    }
}
