namespace SchoolManagementSystem.Domain.Entities.Identity;

public class RoleModulePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int ModuleId { get; set; }
    public Module Module { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
