namespace SchoolManagementSystem.Domain.Abstractions;

/// <summary>
/// Per-request campus access, loaded from the database by the API's campus-scope middleware
/// right after authentication. Nothing here is taken from the token or headers without validation.
/// </summary>
public sealed class CampusScope
{
    private static readonly IReadOnlySet<int> None = new HashSet<int>();

    public bool IsResolved { get; private set; }

    public bool IsHq { get; private set; }

    public bool IsSuperAdmin { get; private set; }

    public int? ActiveCampusId { get; private set; }

    public IReadOnlySet<int> AssignedCampusIds { get; private set; } = None;

    public void Resolve(bool isHq, bool isSuperAdmin, IReadOnlySet<int> assignedCampusIds, int? activeCampusId)
    {
        IsHq = isHq || isSuperAdmin;
        IsSuperAdmin = isSuperAdmin;
        AssignedCampusIds = assignedCampusIds;
        ActiveCampusId = activeCampusId;
        IsResolved = true;
    }
}
