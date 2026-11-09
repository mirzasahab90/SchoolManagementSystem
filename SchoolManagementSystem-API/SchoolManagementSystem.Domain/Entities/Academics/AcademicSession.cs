using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Domain.Entities.Academics;

/// <summary>An academic year, e.g. 2026-27. HQ-owned (all campuses) or owned by one campus.</summary>
public class AcademicSession : AuditableEntity, ICampusOwnable
{
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Planned;

    /// <summary>Current session of its owner (HQ, or the owning campus).</summary>
    public bool IsCurrent { get; set; }

    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
}
