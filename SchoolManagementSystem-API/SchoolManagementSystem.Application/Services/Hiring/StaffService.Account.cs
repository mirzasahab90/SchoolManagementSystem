using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.DTOs.Users;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>The staff member's login. Creation goes through IUserService, so every role and campus rule of the Users page applies.</summary>
public partial class StaffService
{
    private static readonly Error AlreadyHasLogin = Error.Conflict("This staff member already has a login.");

    public async Task<Result<StaffProfileDto>> CreateLoginAsync(int id, CreateStaffLoginRequest request, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        if (!IsCurrent(staff.Status)) return Error.Validation("This staff member has left.");
        if (staff.UserId != null) return AlreadyHasLogin;
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Error.Validation("Email and a temporary password are required.");

        var created = await userService.CreateAsync(new SaveUserRequest(
            staff.FirstName, staff.LastName, request.Email.Trim(), staff.Phone, IsActive: true, IsHq: false,
            RoleIds: [request.RoleId], CampusIds: [staff.CampusId], Password: request.Password, MustChangePassword: true), ct);
        if (!created.IsSuccess) return created.Error!;

        staff.UserId = created.Value!.Id;
        TryActivate(staff);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }

    public async Task<Result<StaffProfileDto>> LinkLoginAsync(int id, LinkStaffLoginRequest request, CancellationToken ct = default)
    {
        var staff = await repositories.Staff.GetTrackedAsync(id, ct);
        if (staff == null) return Error.NotFound("Staff member");
        if (!IsCurrent(staff.Status)) return Error.Validation("This staff member has left.");
        if (staff.UserId != null) return AlreadyHasLogin;

        var user = await repositories.Users.GetWithRolesAndCampusesAsync(request.UserId, ct);
        if (user == null || !user.IsActive || user.IsHq || user.UserCampuses.All(c => c.CampusId != staff.CampusId))
            return Error.Validation("Pick an active user of this campus.");
        if (user.Id == currentUser.UserId) return Error.Forbidden("You cannot link your own account.");
        // Same rules as changing the user on the Users page: resigning this member later deactivates the login.
        var guard = await userService.CheckCanManageAsync(user.Id, ct);
        if (guard != null) return guard.Status == 404 ? Error.Forbidden("You cannot manage this user.") : guard;
        if (await repositories.Staff.UserLinkedAsync(user.Id, staff.Id, ct))
            return Error.Conflict("This user is already linked to another staff member.");

        // A rehired person's old, left record of this campus gives the login up (the unique index allows one link).
        foreach (var old in await repositories.Staff.LeftRecordsLinkedToAsync(user.Id, staff.CampusId, ct)) old.UserId = null;
        await repositories.SaveChangesAsync(ct);

        staff.UserId = user.Id;
        TryActivate(staff);
        await repositories.SaveChangesAsync(ct);
        return await GetProfileAsync(id, ct);
    }
}
