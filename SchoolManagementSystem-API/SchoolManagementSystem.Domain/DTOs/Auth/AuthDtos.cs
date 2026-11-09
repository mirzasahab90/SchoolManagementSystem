namespace SchoolManagementSystem.Domain.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record TokenResult(string AccessToken, DateTime AccessExpiresUtc, string RefreshToken, DateTime RefreshExpiresUtc);

public record AuthResult(TokenResult Tokens, CurrentUserDto User);

public record MenuItemDto(string Key, string Name, string? Route, string? Icon, int DisplayOrder, IReadOnlyList<MenuItemDto> Children);

public record CampusOptionDto(int Id, string Code, string Name, bool IsPrimary);

public record CurrentUserDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    bool IsHq,
    bool IsSuperAdmin,
    IReadOnlyList<string> Roles,
    bool MustChangePassword,
    IReadOnlyList<MenuItemDto> Menu,
    IReadOnlyList<CampusOptionDto> Campuses,
    int? ActiveCampusId,
    string? SchoolName,
    string? SchoolShortName,
    /// <summary>Module key → granted action codes (View/Add/Edit/Delete…). SuperAdmin may be empty; treat as full access.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Permissions);

/// <summary>Database view of what campuses a user may work in (HQ = every campus).</summary>
public record UserCampusAccessDto(bool IsHq, bool IsSuperAdmin, IReadOnlyList<int> CampusIds, bool MustChangePassword = false);

public record UserAccessDto(bool IsSuperAdmin, IReadOnlyList<string> Roles, IReadOnlyDictionary<string, HashSet<string>> ModuleActions)
{
    public bool Can(string moduleKey, string action) =>
        IsSuperAdmin || (ModuleActions.TryGetValue(moduleKey, out var actions) && actions.Contains(action));
}
