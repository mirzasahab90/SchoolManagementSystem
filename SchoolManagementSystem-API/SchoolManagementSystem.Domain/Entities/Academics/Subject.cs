using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Domain.Entities.Academics;

public class Subject : AuditableEntity, ICampusOwnable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public SubjectType Type { get; set; }
    public bool IsActive { get; set; } = true;

    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
}
