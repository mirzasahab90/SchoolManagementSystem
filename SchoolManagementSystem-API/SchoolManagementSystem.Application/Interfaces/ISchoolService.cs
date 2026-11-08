using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Org;

namespace SchoolManagementSystem.Application.Interfaces;

public interface ISchoolService
{
    Task<Result<SchoolDto>> GetAsync(CancellationToken ct = default);

    Task<Result<SchoolDto>> UpdateAsync(SaveSchoolRequest request, CancellationToken ct = default);
}
