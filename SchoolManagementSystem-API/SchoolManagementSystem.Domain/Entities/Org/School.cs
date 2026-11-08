using SchoolManagementSystem.Domain.Common;

namespace SchoolManagementSystem.Domain.Entities.Org;

public class School : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    public ICollection<Campus> Campuses { get; set; } = new List<Campus>();
}
