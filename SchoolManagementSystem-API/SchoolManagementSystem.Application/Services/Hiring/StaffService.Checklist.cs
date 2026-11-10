using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>The fixed four-item onboarding checklist.</summary>
public partial class StaffService
{
    /// <summary>CNIC for everyone; a Degree too for teaching staff.</summary>
    internal static IReadOnlyList<StaffDocumentType> RequiredDocuments(Staff staff) =>
        staff.Category == StaffCategory.Teaching ? [StaffDocumentType.Cnic, StaffDocumentType.Degree] : [StaffDocumentType.Cnic];

    internal static ChecklistDto BuildChecklist(Staff staff)
    {
        var present = staff.Documents.Where(d => !d.IsDeleted).Select(d => d.Type).ToHashSet();
        var missing = RequiredDocuments(staff).Where(t => !present.Contains(t)).ToList();
        var documents = missing.Count == 0;
        var login = staff.UserId != null;
        var done = (documents ? 1 : 0) + (staff.ContractSigned ? 1 : 0) + (login ? 1 : 0) + (staff.OrientationDone ? 1 : 0);
        return new ChecklistDto(documents, missing, staff.ContractSigned, staff.ContractSignedOn, login,
            staff.OrientationDone, staff.OrientationDoneOn, done);
    }

    /// <summary>Onboarding → Active once all four items are done. Never moves an Active member back. Caller saves.</summary>
    internal static void TryActivate(Staff staff)
    {
        if (staff.Status == StaffStatus.Onboarding && BuildChecklist(staff).DoneCount == 4) staff.Status = StaffStatus.Active;
    }

    /// <summary>Ticks or unticks a manual item: "contract" or "orientation". The other two are automatic.</summary>
    public async Task<Result<StaffProfileDto>> SetChecklistAsync(int id, string item, bool done, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        if (!IsCurrent(staff.Status)) return Error.Validation("This staff member has left.");

        switch (item.ToLowerInvariant())
        {
            case "contract":
                staff.ContractSigned = done;
                staff.ContractSignedOn = done ? DateTime.UtcNow : null;
                break;
            case "orientation":
                staff.OrientationDone = done;
                staff.OrientationDoneOn = done ? DateTime.UtcNow : null;
                break;
            default:
                return Error.Validation("Only Contract signed and Orientation done are ticked by hand; the others follow documents and the login.");
        }

        TryActivate(staff);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }
}
