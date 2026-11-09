namespace SchoolManagementSystem.Domain.Abstractions;

/// <summary>The signed-in user of the current request (null for background jobs and anonymous calls).</summary>
public interface ICurrentUser
{
    int? UserId { get; }

    /// <summary>
    /// Working campus, validated against the user's access by the campus-scope middleware.
    /// Null for an HQ user working across "All campuses" (or a user with no campus).
    /// </summary>
    int? ActiveCampusId { get; }

    /// <summary>True when the user may see all campuses (SuperAdmin / HQ). Read from the database each request.</summary>
    bool IsHq { get; }

    bool IsSuperAdmin { get; }

    /// <summary>Active campuses assigned to the user (empty for anonymous calls).</summary>
    IReadOnlySet<int> AssignedCampusIds { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }

    /// <summary>True when the user may read or change data of this campus.</summary>
    bool CanAccessCampus(int campusId) => IsHq || AssignedCampusIds.Contains(campusId);

    /// <summary>Campuses the user may administer; null means every campus (HQ).</summary>
    IReadOnlySet<int>? ManageableCampusIds => IsHq ? null : AssignedCampusIds;

    /// <summary>
    /// Campuses whose business data (students, fees, …) this request works on; null means every campus.
    /// A selected working campus narrows HQ users too.
    /// </summary>
    IReadOnlyCollection<int>? DataCampusIds =>
        ActiveCampusId is int active ? [active] : IsHq ? null : AssignedCampusIds;
}
