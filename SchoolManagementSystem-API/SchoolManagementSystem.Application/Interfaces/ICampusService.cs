using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Org;

namespace SchoolManagementSystem.Application.Interfaces;

public interface ICampusService
{
    Task<Result<IReadOnlyList<CampusDto>>> ListAsync(CancellationToken ct = default);

    Task<Result<CampusDto>> GetAsync(int id, CancellationToken ct = default);

    Task<Result<CampusDto>> CreateAsync(SaveCampusRequest request, CancellationToken ct = default);

    Task<Result<CampusDto>> UpdateAsync(int id, SaveCampusRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
