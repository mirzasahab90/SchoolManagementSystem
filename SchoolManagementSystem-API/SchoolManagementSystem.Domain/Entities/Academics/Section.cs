using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Domain.Entities.Academics;

/// <summary>A section of a class on one campus for one session, e.g. Grade 5 – A.</summary>
public class Section : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;

    public int AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;

    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public SectionShift Shift { get; set; } = SectionShift.Morning;
    public int? Capacity { get; set; }
    public string? Room { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Staff member who is this section's class teacher.</summary>
    public int? ClassTeacherStaffId { get; set; }
    public Hiring.Staff? ClassTeacher { get; set; }
}
