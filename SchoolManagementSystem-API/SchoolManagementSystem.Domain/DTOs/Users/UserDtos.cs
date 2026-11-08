namespace SchoolManagementSystem.Domain.DTOs.Users;

public record RoleRefDto(int Id, string Name);

public record UserListItemDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    string? Phone,
    bool IsActive,
    bool IsHq,
    DateTime? LastLoginOn,
    IReadOnlyList<RoleRefDto> Roles,
    IReadOnlyList<int> CampusIds);

public record SaveUserRequest(
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    bool IsActive,
    bool IsHq,
    IReadOnlyList<int> RoleIds,
    IReadOnlyList<int> CampusIds,
    string? Password);
