namespace SchoolManagementSystem.Domain.Abstractions;

/// <summary>The signed-in user of the current request (null for background jobs and anonymous calls).</summary>
public interface ICurrentUser
{
    int? UserId { get; }

    /// <summary>Active campus from X-Campus-Id header when valid for this user.</summary>
    int? ActiveCampusId { get; }

    /// <summary>True when the user may see all campuses (SuperAdmin / HQ).</summary>
    bool IsHq { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}
