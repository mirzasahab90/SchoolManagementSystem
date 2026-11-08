using SchoolManagementSystem.Domain.Common;

namespace SchoolManagementSystem.Domain.Entities.Identity;

public class Permission : Entity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
