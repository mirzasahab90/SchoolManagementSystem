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

public class UserService(
    SchoolManagementSystemRepositories repositories,
    IPasswordHasher<User> passwordHasher,
    ICurrentUser currentUser,
    IPermissionChecker permissionChecker) : IUserService
{
    public async Task<Result<IReadOnlyList<UserListItemDto>>> ListAsync(CancellationToken ct = default)
    {
        var list = await repositories.Users.ListAsync(ct);
        return list;
    }

    public async Task<Result<UserListItemDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var item = await repositories.Users.GetListItemAsync(id, ct);
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
            MustChangePassword = string.IsNullOrWhiteSpace(request.Password)
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

        var validation = await ValidateAsync(request, id, ct);
        if (validation != null)
        {
            return validation;
        }

        if (id == currentUser.UserId && !request.IsActive)
        {
            return Error.Validation("You cannot deactivate your own account.");
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

        var user = await repositories.Users.GetByIdAsync(id, ct);
        if (user == null)
        {
            return Error.NotFound("User");
        }

        repositories.Users.Remove(user);
        await repositories.SaveChangesAsync(ct);
        permissionChecker.Invalidate();
        return Result.Ok("User deleted.");
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

        if (!request.IsHq && request.CampusIds.Count == 0)
        {
            return Error.Validation("Assign at least one campus, or mark the user as HQ.");
        }

        if (request.CampusIds.Count > 0)
        {
            var campuses = await repositories.Campuses.ListAsync(ct);
            var valid = campuses.Select(c => c.Id).ToHashSet();
            if (request.CampusIds.Any(id => !valid.Contains(id)))
            {
                return Error.Validation("One or more campuses are invalid.");
            }
        }

        if (excludeId is null && string.IsNullOrWhiteSpace(request.Password))
        {
            return Error.Validation("Password is required for new users.");
        }

        if (!string.IsNullOrWhiteSpace(request.Password) && request.Password.Length < 6)
        {
            return Error.Validation("Password must be at least 6 characters.");
        }

        // Only SuperAdmin may assign SuperAdmin role.
        var assigningSuperAdmin = roles.Any(r => r.Name == SystemRoles.SuperAdmin);
        if (assigningSuperAdmin && currentUser.UserId is int actorId)
        {
            var actorAccess = await repositories.Access.GetUserAccessAsync(actorId, ct);
            if (!actorAccess.IsSuperAdmin)
            {
                return Error.Forbidden("Only SuperAdmin can assign the SuperAdmin role.");
            }
        }

        return null;
    }
}
