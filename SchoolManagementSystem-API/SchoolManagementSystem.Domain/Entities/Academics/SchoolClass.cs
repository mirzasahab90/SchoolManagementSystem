using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Domain.Entities.Academics;

/// <summary>A class / grade such as "Grade 5". Named SchoolClass because "class" is a C# keyword.</summary>
public class SchoolClass : AuditableEntity, ICampusOwnable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ClassStage Stage { get; set; }

    /// <summary>Promotion order: Playgroup first, Grade 10 last.</summary>
    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }

    public ICollection<ClassSubject> Subjects { get; set; } = new List<ClassSubject>();
}
