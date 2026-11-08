using SchoolManagementSystem.Domain.Common;

namespace SchoolManagementSystem.Domain.Entities.Org;

public class Campus : AuditableEntity
{
    public int SchoolId { get; set; }
    public School School { get; set; } = null!;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Street / area address.</summary>
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }

    /// <summary>Campus head / principal in charge.</summary>
    public string? InChargeName { get; set; }
    public string? InChargePhone { get; set; }
    public string? InChargeEmail { get; set; }
    public string? InChargeDesignation { get; set; }

    public DateOnly? EstablishedOn { get; set; }
    public int? StudentCapacity { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UserCampus> UserCampuses { get; set; } = new List<UserCampus>();
}
