using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Application.Services.Academics;

/// <summary>Subjects: HQ defaults for every campus plus each campus's own.</summary>
public class SubjectService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : ISubjectService
{
    public async Task<Result<IReadOnlyList<SubjectDto>>> ListAsync(CancellationToken ct = default) =>
        await repositories.Subjects.ListAsync(ct);

    public async Task<Result<SubjectDto>> CreateAsync(SaveSubjectRequest request, CancellationToken ct = default)
    {
        var ownerError = Ownership.ResolveOwner(currentUser, request.CampusId, out var owner);
        if (ownerError != null) return ownerError;

        var validation = await ValidateAsync(request, owner, null, ct);
        if (validation != null) return validation;

        var subject = new Subject { CampusId = owner };
        Apply(subject, request);
        repositories.Subjects.Add(subject);
        await repositories.SaveChangesAsync(ct);
        return await GetDtoAsync(subject.Id, ct);
    }

    public async Task<Result<SubjectDto>> UpdateAsync(int id, SaveSubjectRequest request, CancellationToken ct = default)
    {
        var subject = await repositories.Subjects.GetTrackedAsync(id, ct);
        if (subject == null) return Error.NotFound("Subject");

        var writeError = Ownership.CheckWrite(currentUser, subject.CampusId, "Subject");
        if (writeError != null) return writeError;

        var validation = await ValidateAsync(request, subject.CampusId, id, ct);
        if (validation != null) return validation;

        Apply(subject, request);
        await repositories.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var subject = await repositories.Subjects.GetTrackedAsync(id, ct);
        if (subject == null) return Error.NotFound("Subject");

        var writeError = Ownership.CheckWrite(currentUser, subject.CampusId, "Subject");
        if (writeError != null) return writeError;

        var used = await repositories.Subjects.CountClassesUsingAsync(id, ct);
        if (used > 0) return Error.Validation($"Subject is used by {used} class(es). Deactivate it instead.");

        repositories.Subjects.Remove(subject);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Subject deleted.");
    }

    private async Task<Error?> ValidateAsync(SaveSubjectRequest request, int? owner, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return Error.Validation("Code is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return Error.Validation("Name is required.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repositories.Subjects.CodeTakenAsync(code, owner, excludeId, ct))
            return Error.Conflict($"Subject code '{code}' already exists.");
        return null;
    }

    private static void Apply(Subject subject, SaveSubjectRequest request)
    {
        subject.Code = request.Code.Trim().ToUpperInvariant();
        subject.Name = request.Name.Trim();
        subject.Type = request.Type;
        subject.IsActive = request.IsActive;
    }

    private async Task<Result<SubjectDto>> GetDtoAsync(int id, CancellationToken ct)
    {
        var dto = (await repositories.Subjects.ListAsync(ct)).FirstOrDefault(s => s.Id == id);
        return dto is null ? Error.NotFound("Subject") : dto;
    }
}
