using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Domain.Entities.Identity;

public class Role : AuditableEntity, ICampusOwnable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystem { get; set; }

    /// <summary>Only HQ may assign this role (SuperAdmin, CampusAdmin).</summary>
    public bool HqAssignOnly { get; set; }

    /// <summary>Null = head-office role usable on every campus; otherwise owned by one campus.</summary>
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RoleModulePermission> Permissions { get; set; } = new List<RoleModulePermission>();
}
