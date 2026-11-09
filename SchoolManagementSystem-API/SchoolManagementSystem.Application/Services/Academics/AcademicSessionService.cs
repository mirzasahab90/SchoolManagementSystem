using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Application.Services.Academics;

/// <summary>
/// Academic years. HQ sessions apply to every campus; a campus may add its own and may set one of them
/// as current, which overrides the HQ current session for that campus only.
/// </summary>
public class AcademicSessionService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser)
    : IAcademicSessionService
{
    public async Task<Result<AcademicSessionListDto>> ListAsync(CancellationToken ct = default)
    {
        var items = await repositories.Sessions.ListAsync(ct);
        var effective = await repositories.Sessions.GetEffectiveCurrentAsync(currentUser.ActiveCampusId, ct);
        return new AcademicSessionListDto(items, effective?.Id);
    }

    public async Task<Result<AcademicSessionDto>> CreateAsync(SaveAcademicSessionRequest request, CancellationToken ct = default)
    {
        var ownerError = Ownership.ResolveOwner(currentUser, request.CampusId, out var owner);
        if (ownerError != null) return ownerError;

        var validation = await ValidateAsync(request, owner, null, ct);
        if (validation != null) return validation;

        var session = new AcademicSession
        {
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status,
            CampusId = owner
        };
        repositories.Sessions.Add(session);
        await repositories.SaveChangesAsync(ct);
        return await GetDtoAsync(session.Id, ct);
    }

    public async Task<Result<AcademicSessionDto>> UpdateAsync(int id, SaveAcademicSessionRequest request, CancellationToken ct = default)
    {
        var session = await repositories.Sessions.GetTrackedAsync(id, ct);
        if (session == null) return Error.NotFound("Session");

        var writeError = Ownership.CheckWrite(currentUser, session.CampusId, "Session");
        if (writeError != null) return writeError;

        var validation = await ValidateAsync(request, session.CampusId, id, ct);
        if (validation != null) return validation;

        if (session.IsCurrent && request.Status != SessionStatus.Active)
            return Error.Validation("The current session must stay active. Make another session current first.");

        session.Name = request.Name.Trim();
        session.StartDate = request.StartDate;
        session.EndDate = request.EndDate;
        session.Status = request.Status;
        await repositories.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var session = await repositories.Sessions.GetTrackedAsync(id, ct);
        if (session == null) return Error.NotFound("Session");

        var writeError = Ownership.CheckWrite(currentUser, session.CampusId, "Session");
        if (writeError != null) return writeError;

        if (session.IsCurrent)
            return Error.Validation("The current session cannot be deleted. Make another session current first.");
        if (await repositories.Sessions.HasSectionsAsync(id, ct))
            return Error.Validation("This session has sections. Delete them or keep the session.");

        repositories.Sessions.Remove(session);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Session deleted.");
    }

    public async Task<Result> SetCurrentAsync(int id, CancellationToken ct = default)
    {
        var session = await repositories.Sessions.GetTrackedAsync(id, ct);
        if (session == null) return Error.NotFound("Session");

        // A campus user choosing an HQ session drops their campus override and follows HQ again.
        if (session.CampusId == null && !currentUser.IsHq)
        {
            if (currentUser.ActiveCampusId is not int campusId) return Error.Validation("Select a campus.");
            foreach (var own in await repositories.Sessions.GetCurrentForOwnerAsync(campusId, ct))
            {
                own.IsCurrent = false;
            }

            await repositories.SaveChangesAsync(ct);
            return Result.Ok("Following the head-office session.");
        }

        var writeError = Ownership.CheckWrite(currentUser, session.CampusId, "Session");
        if (writeError != null) return writeError;

        if (session.Status == SessionStatus.Closed)
            return Error.Validation("Reopen the session first.");

        // Two saves in one transaction: the "one current per owner" index must never see two at once.
        await using var tx = await repositories.BeginTransactionAsync(ct);
        foreach (var other in await repositories.Sessions.GetCurrentForOwnerAsync(session.CampusId, ct))
        {
            other.IsCurrent = false;
        }

        await repositories.SaveChangesAsync(ct);
        session.IsCurrent = true;
        session.Status = SessionStatus.Active;
        await repositories.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result.Ok("Current session updated.");
    }

    private async Task<Error?> ValidateAsync(SaveAcademicSessionRequest request, int? owner, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Error.Validation("Session name is required.");
        if (request.EndDate <= request.StartDate) return Error.Validation("End date must be after the start date.");

        var overlap = await repositories.Sessions.FindOverlapAsync(request.StartDate, request.EndDate, owner, excludeId, ct);
        if (overlap != null) return Error.Validation($"Dates overlap with session '{overlap.Name}'.");

        var name = request.Name.Trim();
        if (await repositories.Sessions.NameTakenAsync(name, owner, excludeId, ct))
            return Error.Conflict($"Session '{name}' already exists.");
        return null;
    }

    private async Task<Result<AcademicSessionDto>> GetDtoAsync(int id, CancellationToken ct)
    {
        var dto = (await repositories.Sessions.ListAsync(ct)).FirstOrDefault(s => s.Id == id);
        return dto is null ? Error.NotFound("Session") : dto;
    }
}
