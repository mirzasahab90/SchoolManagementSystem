using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Hiring;

namespace SchoolManagementSystem.Application.Services.Hiring;

/// <summary>Id and name of the working campus's active users (interviewer picker).</summary>
public interface IUserPicker
{
    /// <summary>For the candidate's campus when given (visible candidates only), else the working campus.</summary>
    Task<Result<IReadOnlyList<UserPickDto>>> PickAsync(int? candidateId, CancellationToken ct = default);

    Task<bool> CanAsync(string module, string action, CancellationToken ct = default);
}

public class UserPicker(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser, IPermissionChecker permissions) : IUserPicker
{
    public async Task<Result<IReadOnlyList<UserPickDto>>> PickAsync(int? candidateId, CancellationToken ct = default)
    {
        int campusId;
        if (candidateId is int cid)
        {
            var candidate = await repositories.Candidates.GetTrackedAsync(cid, ct);
            if (candidate == null) return Error.NotFound("Candidate");
            campusId = candidate.CampusId;
        }
        else if (currentUser.ActiveCampusId is int active)
        {
            campusId = active;
        }
        else
        {
            return Error.Validation("Select a campus.");
        }

        return await repositories.Users.PickAsync(campusId, ct);
    }

    public async Task<bool> CanAsync(string module, string action, CancellationToken ct = default) =>
        currentUser.UserId is int uid && await permissions.HasPermissionAsync(uid, module, action, ct);
}
