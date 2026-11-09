using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Application.Utilities.Security;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Configuration;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs.Auth;
using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Application.Services.Auth;

public class AuthService(
    SchoolManagementSystemRepositories repositories,
    TokenService tokenService,
    IPasswordHasher<User> passwordHasher,
    IOptions<AuthOptions> authOptions) : IAuthService
{
    private static readonly Error InvalidLogin = Error.Unauthorized("Invalid email or password.");
    private static readonly Error SessionExpired = Error.Unauthorized("Session expired. Please sign in again.");
    private readonly AuthOptions _auth = authOptions.Value;

    public async Task<Result<AuthResult>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await repositories.Users.GetByEmailAsync(Normalize(request.Email), ct);
        if (user?.PasswordHash == null)
        {
            return InvalidLogin;
        }

        if (user.LockoutEndUtc > DateTime.UtcNow)
        {
            return Error.Unauthorized($"Account is locked. Try again after {_auth.LockoutMinutes} minutes.");
        }

        if (!user.IsActive)
        {
            return Error.Unauthorized("Your account is inactive. Please contact your administrator.");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _auth.MaxFailedAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(_auth.LockoutMinutes);
                user.FailedLoginCount = 0;
            }

            await repositories.SaveChangesAsync(ct);
            return InvalidLogin;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        user.FailedLoginCount = 0;
        user.LockoutEndUtc = null;
        user.LastLoginOn = DateTime.UtcNow;
        await repositories.SaveChangesAsync(ct);

        return await IssueAsync(user, null, ct);
    }

    public async Task<AuthResult?> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var user = await tokenService.ConsumeRefreshTokenAsync(refreshToken, ct);
        return user == null ? null : await IssueAsync(user, null, ct);
    }

    public Task LogoutAsync(string? refreshToken, CancellationToken ct = default) =>
        string.IsNullOrWhiteSpace(refreshToken) ? Task.CompletedTask : tokenService.RevokeAsync(refreshToken, ct);

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(int userId, int? activeCampusId, CancellationToken ct = default)
    {
        var user = await repositories.Users.GetActiveReadOnlyAsync(userId, ct);
        if (user == null)
        {
            return SessionExpired;
        }

        return await BuildCurrentUserAsync(user, activeCampusId, ct);
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private async Task<AuthResult> IssueAsync(User user, int? activeCampusId, CancellationToken ct)
    {
        var currentUser = await BuildCurrentUserAsync(user, activeCampusId, ct);
        var tokens = await tokenService.IssueAsync(user, currentUser.Roles, ct);
        return new AuthResult(tokens, currentUser);
    }

    private async Task<CurrentUserDto> BuildCurrentUserAsync(User user, int? activeCampusId, CancellationToken ct)
    {
        var access = await repositories.Access.GetUserAccessAsync(user.Id, ct);
        var modules = await repositories.Access.GetActiveModulesAsync(ct);
        var menu = MenuBuilder.Build(modules, access);
        var isSuperAdmin = access.IsSuperAdmin || access.Roles.Contains(SystemRoles.SuperAdmin);
        var isHq = user.IsHq || isSuperAdmin;

        List<CampusOptionDto> campusOptions;
        if (isHq)
        {
            var all = await repositories.Campuses.ListAsync(null, ct);
            var assigned = await repositories.Users.GetUserCampusesAsync(user.Id, ct);
            var primaryIds = assigned.Where(a => a.IsPrimary).Select(a => a.CampusId).ToHashSet();
            campusOptions = all
                .Where(c => c.IsActive)
                .Select(c => new CampusOptionDto(c.Id, c.Code, c.Name, primaryIds.Contains(c.Id)))
                .ToList();
        }
        else
        {
            var campuses = await repositories.Users.GetUserCampusesAsync(user.Id, ct);
            campusOptions = campuses.Select(c => new CampusOptionDto(c.CampusId, c.Code, c.Name, c.IsPrimary)).ToList();
        }

        // HQ may work on "All campuses" (null). Campus users always get one of their own campuses.
        int? resolvedCampus = activeCampusId is int requested && campusOptions.Any(c => c.Id == requested)
            ? requested
            : null;
        if (!isHq)
        {
            resolvedCampus ??= campusOptions.FirstOrDefault(c => c.IsPrimary)?.Id
                               ?? campusOptions.FirstOrDefault()?.Id;
        }

        var school = await repositories.Schools.GetDtoAsync(ct);
        var permissions = access.ModuleActions.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value.OrderBy(a => a).ToList(),
            StringComparer.OrdinalIgnoreCase);

        return new CurrentUserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Email,
            isHq,
            isSuperAdmin,
            access.Roles,
            user.MustChangePassword,
            menu,
            campusOptions,
            resolvedCampus,
            school?.Name,
            school?.ShortName,
            permissions);
    }
}
