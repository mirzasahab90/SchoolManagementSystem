namespace SchoolManagementSystem.Domain.DTOs.Org;

public record RoleListItemDto(
    int Id, string Name, string? Description, bool IsSystem, bool IsActive, int UserCount, int PermissionCount,
    int? CampusId, string? CampusCode, bool HqAssignOnly);

public record ModulePermissionCellDto(string ModuleKey, string ModuleName, bool View, bool Add, bool Edit, bool Delete);

public record RoleDetailDto(
    int Id,
    string Name,
    string? Description,
    bool IsSystem,
    bool IsActive,
    IReadOnlyList<ModulePermissionCellDto> Matrix);

public record SaveRolePermissionsRequest(
    string Name,
    string? Description,
    bool IsActive,
    IReadOnlyList<ModulePermissionCellDto> Matrix);

public record CreateRoleRequest(string Name, string? Description);
