using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Application.Services.Academics;

/// <summary>
/// Classes and their subject lists. HQ classes are shared and HQ-only to change; a campus may add its own
/// classes, whose subjects may be HQ subjects or that campus's own.
/// </summary>
public class ClassService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IClassService
{
    public async Task<Result<IReadOnlyList<ClassDto>>> ListAsync(CancellationToken ct = default) =>
        await repositories.Classes.ListAsync(ct);

    public async Task<Result<ClassDetailDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var detail = await repositories.Classes.GetDetailAsync(id, ct);
        return detail is null ? Error.NotFound("Class") : detail;
    }

    public async Task<Result<ClassDetailDto>> CreateAsync(SaveClassRequest request, CancellationToken ct = default)
    {
        var ownerError = Ownership.ResolveOwner(currentUser, request.CampusId, out var owner);
        if (ownerError != null) return ownerError;

        var validation = await ValidateAsync(request, owner, null, ct);
        if (validation != null) return validation;

        var schoolClass = new SchoolClass { CampusId = owner };
        Apply(schoolClass, request);
        repositories.Classes.Add(schoolClass);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(schoolClass.Id, ct);
    }

    public async Task<Result<ClassDetailDto>> UpdateAsync(int id, SaveClassRequest request, CancellationToken ct = default)
    {
        var schoolClass = await repositories.Classes.GetTrackedAsync(id, ct);
        if (schoolClass == null) return Error.NotFound("Class");

        var writeError = Ownership.CheckWrite(currentUser, schoolClass.CampusId, "Class");
        if (writeError != null) return writeError;

        var validation = await ValidateAsync(request, schoolClass.CampusId, id, ct);
        if (validation != null) return validation;

        Apply(schoolClass, request);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var schoolClass = await repositories.Classes.GetTrackedAsync(id, ct);
        if (schoolClass == null) return Error.NotFound("Class");

        var writeError = Ownership.CheckWrite(currentUser, schoolClass.CampusId, "Class");
        if (writeError != null) return writeError;

        var sections = await repositories.Classes.CountSectionsAsync(id, ct);
        if (sections > 0) return Error.Validation($"Class has {sections} section(s). Deactivate it instead.");

        repositories.Classes.Remove(schoolClass);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Class deleted.");
    }

    public async Task<Result<ClassDetailDto>> SetSubjectsAsync(int id, IReadOnlyList<SaveClassSubjectItem> items, CancellationToken ct = default)
    {
        var schoolClass = await repositories.Classes.GetTrackedWithSubjectsAsync(id, ct);
        if (schoolClass == null) return Error.NotFound("Class");

        var writeError = Ownership.CheckWrite(currentUser, schoolClass.CampusId, "Class");
        if (writeError != null) return writeError;

        var ids = items.Select(i => i.SubjectId).ToList();
        if (ids.Count != ids.Distinct().Count()) return Error.Validation("A subject is listed twice.");

        var subjects = await repositories.Subjects.GetByIdsAsync(ids, ct);
        var usable = subjects.Where(s => s.IsActive && Ownership.CanReference(s.CampusId, schoolClass.CampusId)).ToDictionary(s => s.Id);
        if (usable.Count != ids.Count) return Error.Validation("One or more subjects are invalid.");

        schoolClass.Subjects.Clear();
        var order = 1;
        foreach (var item in items)
        {
            schoolClass.Subjects.Add(new ClassSubject
            {
                ClassId = schoolClass.Id,
                SubjectId = item.SubjectId,
                IsOptional = item.IsOptional,
                DisplayOrder = order++
            });
        }

        await repositories.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<Error?> ValidateAsync(SaveClassRequest request, int? owner, int? excludeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return Error.Validation("Code is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return Error.Validation("Name is required.");
        var code = request.Code.Trim().ToUpperInvariant();
        if (await repositories.Classes.CodeTakenAsync(code, owner, excludeId, ct))
            return Error.Conflict($"Class code '{code}' already exists.");
        return null;
    }

    private static void Apply(SchoolClass schoolClass, SaveClassRequest request)
    {
        schoolClass.Code = request.Code.Trim().ToUpperInvariant();
        schoolClass.Name = request.Name.Trim();
        schoolClass.Stage = request.Stage;
        schoolClass.DisplayOrder = request.DisplayOrder;
        schoolClass.IsActive = request.IsActive;
    }
}
