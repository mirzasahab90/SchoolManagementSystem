namespace SchoolManagementSystem.Domain.Common;

/// <summary>Base for business tables: who/when created and modified, plus soft delete.</summary>
public abstract class AuditableEntity : Entity
{
    public DateTime CreatedOn { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public int? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
}
