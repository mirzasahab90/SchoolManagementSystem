using Microsoft.AspNetCore.Identity;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Users;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Application.Services.Users;

/// <summary>
/// HQ users manage everyone except SuperAdmins (only a SuperAdmin manages those).
/// Campus users manage only non-HQ users whose campuses all lie within their own campuses,
/// and cannot grant HQ access, foreign campuses, or change their own roles / campuses.
/// </summary>
public class UserService(
    SchoolManagementSystemRepositories repositories,
    IPasswordHasher<User> passwordHasher,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : IUserService
{
    public async Task<Result<IReadOnlyList<UserListItemDto>>> ListAsync(CancellationToken ct = default) =>
        await repositories.Users.ListAsync(currentUser.ManageableCampusIds, null, ct);

    public async Task<Result<UserListItemDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var item = await repositories.Users.GetListItemAsync(id, currentUser.ManageableCampusIds, ct);
        return item is null ? Error.NotFound("User") : item;
    }

    public async Task<Result<UserListItemDto>> CreateAsync(SaveUserRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, null, ct);
        if (validation != null)
        {
            return validation;
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = new User
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            IsActive = request.IsActive,
            IsHq = request.IsHq,
            MustChangePassword = request.MustChangePassword || string.IsNullOrWhiteSpace(request.Password)
        };

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        foreach (var roleId in request.RoleIds.Distinct())
        {
            user.UserRoles.Add(new UserRole
            {
                RoleId = roleId,
                AssignedOn = DateTime.UtcNow,
                AssignedBy = currentUser.UserId
            });
        }

        var primary = true;
        foreach (var campusId in request.CampusIds.Distinct())
        {
            user.UserCampuses.Add(new UserCampus
            {
                CampusId = campusId,
                IsPrimary = primary
            });
            primary = false;
        }

        repositories.Users.Add(user);
        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return await GetAsync(user.Id, ct);
    }

    public async Task<Result<UserListItemDto>> UpdateAsync(int id, SaveUserRequest request, CancellationToken ct = default)
    {
        var user = await repositories.Users.GetWithRolesAndCampusesAsync(id, ct);
        if (user == null)
        {
            return Error.NotFound("User");
        }

        var targetError = await GuardTargetAsync(user, ct);
        if (targetError != null)
        {
            return targetError;
        }

        var validation = await ValidateAsync(request, id, ct);
        if (validation != null)
        {
            return validation;
        }

        if (id == currentUser.UserId)
        {
            if (!request.IsActive)
            {
                return Error.Validation("You cannot deactivate your own account.");
            }

            var sameRoles = user.UserRoles.Select(r => r.RoleId).ToHashSet().SetEquals(request.RoleIds);
            var sameCampuses = user.UserCampuses.Select(c => c.CampusId).ToHashSet().SetEquals(request.CampusIds);
            if (!currentUser.IsSuperAdmin && (!sameRoles || !sameCampuses || user.IsHq != request.IsHq))
            {
                return Error.Forbidden("You cannot change your own roles, campuses or HQ access.");
            }
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = request.Email.Trim().ToLowerInvariant();
        user.Phone = request.Phone?.Trim();
        user.IsActive = request.IsActive;
        user.IsHq = request.IsHq;

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            user.MustChangePassword = false;
        }

        repositories.Users.ReplaceRoles(user, request.RoleIds, currentUser.UserId);
        repositories.Users.ReplaceCampuses(user, request.CampusIds);
        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return await GetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id == currentUser.UserId)
        {
            return Error.Validation("You cannot delete your own account.");
        }

        var user = await repositories.Users.GetWithRolesAndCampusesAsync(id, ct);
        if (user == null)
        {
            return Error.NotFound("User");
        }

        var targetError = await GuardTargetAsync(user, ct);
        if (targetError != null)
        {
            return targetError;
        }

        repositories.Users.Remove(user);
        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return Result.Ok("User deleted.");
    }

    /// <summary>May the current user change or delete this existing user?</summary>
    private async Task<Error?> GuardTargetAsync(User target, CancellationToken ct)
    {
        if (!currentUser.IsHq)
        {
            var allowed = currentUser.AssignedCampusIds;
            var visible = !target.IsHq && target.UserCampuses.Any(c => allowed.Contains(c.CampusId));
            if (!visible)
            {
                return Error.NotFound("User");
            }

            if (target.UserCampuses.Any(c => !allowed.Contains(c.CampusId)))
            {
                return Error.Forbidden("This user also belongs to campuses you do not manage. Ask HQ to change them.");
            }
        }

        if (!currentUser.IsHq && target.Id != currentUser.UserId)
        {
            var targetRoleIds = target.UserRoles.Select(r => r.RoleId).ToList();
            var targetRoles = await repositories.Users.GetRolesByIdsAsync(targetRoleIds, ct);
            if (targetRoles.Any(r => r.HqAssignOnly))
            {
                return Error.Forbidden("Only HQ can change or remove a campus admin.");
            }

            if (!await WithinMyAccessAsync(targetRoleIds, ct))
            {
                return Error.Forbidden("You cannot change a user who has more access than you.");
            }
        }

        if (!currentUser.IsSuperAdmin && target.Id != currentUser.UserId)
        {
            var targetAccess = await repositories.Access.GetUserAccessAsync(target.Id, ct);
            if (targetAccess.IsSuperAdmin)
            {
                return Error.Forbidden("Only a SuperAdmin can change another SuperAdmin.");
            }
        }

        return null;
    }

    /// <summary>True when every permission the roles grant is one the current user holds.</summary>
    private async Task<bool> WithinMyAccessAsync(IEnumerable<int> roleIds, CancellationToken ct)
    {
        if (currentUser.UserId is not int actorId) return false;
        var mine = await repositories.Access.GetUserAccessAsync(actorId, ct);
        if (mine.IsSuperAdmin) return true;
        var grants = await repositories.Roles.GetGrantsAsync(roleIds, ct);
        return grants.All(g => mine.Can(g.Module, g.Action));
    }

    private async Task<Error?> ValidateAsync(SaveUserRequest request, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return Error.Validation("First name and last name are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Error.Validation("Email is required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await repositories.Users.EmailExistsAsync(email, excludeId, ct))
        {
            return Error.Conflict("A user with this email already exists.");
        }

        if (request.RoleIds.Count == 0)
        {
            return Error.Validation("Select at least one role.");
        }

        var roles = await repositories.Users.GetRolesByIdsAsync(request.RoleIds, ct);
        if (roles.Count != request.RoleIds.Distinct().Count())
        {
            return Error.Validation("One or more roles are invalid.");
        }

        if (roles.Any(r => r.HqAssignOnly) && !currentUser.IsHq)
        {
            return Error.Forbidden("Only HQ can assign this role.");
        }

        // No escalation: outside HQ, the roles given must not carry permissions the editor lacks.
        if (!currentUser.IsHq && !await WithinMyAccessAsync(request.RoleIds, ct))
        {
            return Error.Forbidden("You can only grant permissions you have.");
        }

        // A campus role may only go to users of exactly that campus (never HQ users), so its
        // permissions cannot apply on another campus.
        foreach (var campusRole in roles.Where(r => r.CampusId != null))
        {
            if (request.IsHq || request.CampusIds.Distinct().Count() != 1 || request.CampusIds[0] != campusRole.CampusId)
            {
                return Error.Forbidden($"The role '{campusRole.Name}' belongs to one campus and can only be given to users of that campus.");
            }
        }

        if (request.IsHq && !currentUser.IsHq)
        {
            return Error.Forbidden("Only HQ users can grant HQ access.");
        }

        if (!request.IsHq && request.CampusIds.Count == 0)
        {
            return Error.Validation("Assign at least one campus, or mark the user as HQ.");
        }

        if (request.CampusIds.Count > 0)
        {
            var campuses = await repositories.Campuses.ListAsync(null, ct);
            var valid = campuses.Select(c => c.Id).ToHashSet();
            if (request.CampusIds.Any(id => !valid.Contains(id)))
            {
                return Error.Validation("One or more campuses are invalid.");
            }

            if (request.CampusIds.Any(id => !currentUser.CanAccessCampus(id)))
            {
                return Error.Forbidden("You can only assign campuses you have access to.");
            }
        }

        if (excludeId is null && string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation("Password is required for new users.");
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 8)
        {
            return Error.Validation("Password must be at least 8 characters.");
        }

        if (roles.Any(r => r.Name == SystemRoles.SuperAdmin) && !currentUser.IsSuperAdmin)
        {
            return Error.Forbidden("Only SuperAdmin can assign the SuperAdmin role.");
        }

        return null;
    }
}
