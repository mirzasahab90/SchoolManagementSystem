using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>On leave, back, resign and terminate. Leaving closes the login, the classes and the class-teacher slots.</summary>
public partial class StaffService
{
    public async Task<Result<StaffProfileDto>> SetStatusAsync(int id, StaffStatusRequest request, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");

        var allowed = StatusMoves[staff.Status];
        if (!allowed.Contains(request.Status))
        {
            return Error.Validation(allowed.Length == 0
                ? "This staff member has left; rehire them as a new staff member."
                : $"Allowed next statuses: {string.Join(", ", allowed)}.");
        }

        var leaving = request.Status is StaffStatus.Resigned or StaffStatus.Terminated;
        if (!leaving)
        {
            staff.Status = request.Status;
            await repositories.SaveChangesAsync(ct);
            return await GetProfileAsync(id, ct);
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason == null) return Error.Validation("A reason is required.");
        if (reason.Length > 500) return Error.Validation("The reason is too long (500 characters at most).");
        if (request.Date is not { } date) return Error.Validation("Pick the leaving date.");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (date < staff.JoiningDate) return Error.Validation("The leaving date cannot be before the joining date.");
        if (date > today.AddDays(31)) return Error.Validation("The leaving date can be at most 31 days ahead.");

        if (staff.UserId is int linked && await userService.CheckCanManageAsync(linked, ct) is { } guard && guard.Status != 404)
        {
            return Error.Forbidden("This member's login belongs to a user you cannot manage. Ask Main Campus to record the leaving.");
        }

        await using var tx = await repositories.BeginTransactionAsync(ct);

        staff.Status = request.Status;
        staff.LeftOn = date;
        staff.LeaveReason = reason;

        foreach (var assignment in await repositories.Teaching.CurrentForStaffAsync(staff.Id, ct))
        {
            assignment.EndedOn = date < assignment.StartedOn ? assignment.StartedOn : date;
            assignment.EndReason = "Left";
        }

        foreach (var section in await repositories.Teaching.ClassTeacherSectionsAsync(staff.Id, ct))
        {
            section.ClassTeacherStaffId = null;
        }

        if (staff.UserId is int userId && await repositories.Users.GetByIdAsync(userId, ct) is { } user)
        {
            user.IsActive = false; // the campus-scope middleware rejects inactive users on every request
            await repositories.Tokens.RevokeAllRefreshTokensAsync(userId, DateTime.UtcNow, ct);
        }

        await repositories.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetProfileAsync(id, ct);
    }
}
