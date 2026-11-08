using SchoolManagementSystem.Domain.Common;

namespace SchoolManagementSystem.Domain.Entities.Identity;

public class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RoleModulePermission> Permissions { get; set; } = new List<RoleModulePermission>();
}
