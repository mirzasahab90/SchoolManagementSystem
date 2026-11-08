using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Org;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Application.Services.Org;

public class CampusService(SchoolManagementSystemRepositories repositories) : ICampusService
{
    public async Task<Result<IReadOnlyList<CampusDto>>> ListAsync(CancellationToken ct = default) =>
        await repositories.Campuses.ListAsync(ct);

    public async Task<Result<CampusDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var list = await repositories.Campuses.ListAsync(ct);
        var campus = list.FirstOrDefault(c => c.Id == id);
        return campus is null ? Error.NotFound("Campus") : campus;
    }

    public async Task<Result<CampusDto>> CreateAsync(SaveCampusRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation != null) return validation;

        var code = request.Code.Trim().ToUpperInvariant();
        if (await repositories.Campuses.CodeExistsAsync(code, null, ct))
            return Error.Conflict($"Campus code '{code}' already exists.");

        var school = await repositories.Campuses.GetPrimarySchoolAsync(ct);
        if (school == null)
            return Error.Validation("School profile is missing. Seed the database first.");

        var campus = MapNew(school.Id, code, request);
        repositories.Campuses.Add(campus);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(campus.Id, ct);
    }

    public async Task<Result<CampusDto>> UpdateAsync(int id, SaveCampusRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request);
        if (validation != null) return validation;

        var campus = await repositories.Campuses.GetTrackedAsync(id, ct);
        if (campus == null) return Error.NotFound("Campus");

        var code = request.Code.Trim().ToUpperInvariant();
        if (await repositories.Campuses.CodeExistsAsync(code, id, ct))
            return Error.Conflict($"Campus code '{code}' already exists.");

        Apply(campus, code, request);
        await repositories.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var campus = await repositories.Campuses.GetTrackedAsync(id, ct);
        if (campus == null) return Error.NotFound("Campus");
        repositories.Campuses.Remove(campus);
        await repositories.SaveChangesAsync(ct);
        return Result.Ok("Campus deleted.");
    }

    private static Error? Validate(SaveCampusRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return Error.Validation("Code is required.");
        if (string.IsNullOrWhiteSpace(request.Name)) return Error.Validation("Name is required.");
        if (request.StudentCapacity is < 0) return Error.Validation("Student capacity cannot be negative.");
        return null;
    }

    private static Campus MapNew(int schoolId, string code, SaveCampusRequest request)
    {
        var campus = new Campus { SchoolId = schoolId };
        Apply(campus, code, request);
        return campus;
    }

    private static void Apply(Campus campus, string code, SaveCampusRequest request)
    {
        campus.Code = code;
        campus.Name = request.Name.Trim();
        campus.Address = request.Address?.Trim();
        campus.City = request.City?.Trim();
        campus.State = request.State?.Trim();
        campus.Country = request.Country?.Trim();
        campus.PostalCode = request.PostalCode?.Trim();
        campus.Phone = request.Phone?.Trim();
        campus.Email = request.Email?.Trim();
        campus.Website = request.Website?.Trim();
        campus.InChargeName = request.InChargeName?.Trim();
        campus.InChargePhone = request.InChargePhone?.Trim();
        campus.InChargeEmail = request.InChargeEmail?.Trim();
        campus.InChargeDesignation = request.InChargeDesignation?.Trim();
        campus.EstablishedOn = request.EstablishedOn;
        campus.StudentCapacity = request.StudentCapacity;
        campus.Notes = request.Notes?.Trim();
        campus.IsActive = request.IsActive;
    }
}
