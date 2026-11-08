using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Org;

namespace SchoolManagementSystem.Application.Services.Org;

public class SchoolService(SchoolManagementSystemRepositories repositories) : ISchoolService
{
    public async Task<Result<SchoolDto>> GetAsync(CancellationToken ct = default)
    {
        var school = await repositories.Schools.GetDtoAsync(ct);
        return school is null ? Error.NotFound("School") : school;
    }

    public async Task<Result<SchoolDto>> UpdateAsync(SaveSchoolRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("School name is required.");
        }

        var school = await repositories.Schools.GetTrackedAsync(ct);
        if (school == null)
        {
            return Error.NotFound("School");
        }

        school.Name = request.Name.Trim();
        school.ShortName = request.ShortName?.Trim();
        school.Phone = request.Phone?.Trim();
        school.Email = request.Email?.Trim();
        school.Address = request.Address?.Trim();
        await repositories.SaveChangesAsync(ct);

        return new SchoolDto(school.Id, school.Name, school.ShortName, school.Phone, school.Email, school.Address);
    }
}
