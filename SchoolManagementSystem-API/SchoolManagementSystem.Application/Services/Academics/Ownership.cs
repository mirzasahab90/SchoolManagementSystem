using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;

namespace SchoolManagementSystem.Application.Services.Academics;

/// <summary>
/// Shared rules for head-office (CampusId null) versus campus-owned rows:
/// HQ rows are HQ-only to change; campus rows belong to one campus and are changed by its users or HQ.
/// </summary>
public static class Ownership
{
    public static readonly Error HqItem = Error.Forbidden("Only Main Campus can change Main Campus items.");

    /// <summary>
    /// Owner for a new row. HQ may pass a campus (or none for an HQ row); campus users always create
    /// for their working campus.
    /// </summary>
    public static Error? ResolveOwner(ICurrentUser user, int? requested, out int? owner)
    {
        owner = null;
        if (user.IsHq)
        {
            if (requested is int campusId && !user.CanAccessCampus(campusId))
                return Error.Forbidden("You do not have access to this campus.");
            owner = requested;
            return null;
        }

        if (requested is int asked && asked != user.ActiveCampusId)
            return Error.Forbidden("You can only create items for your own campus.");

        owner = user.ActiveCampusId;
        return owner == null ? Error.Validation("Select a campus.") : null;
    }

    /// <summary>Null when the current user may change a row with this owner.</summary>
    public static Error? CheckWrite(ICurrentUser user, int? owner, string what) =>
        owner is int campusId
            ? user.CanAccessCampus(campusId) ? null : Error.NotFound(what)
            : user.IsHq ? null : HqItem;

    /// <summary>A row may reference HQ rows or rows of the same campus.</summary>
    public static bool CanReference(int? referencedOwner, int? owner) =>
        referencedOwner == null || referencedOwner == owner;
}
