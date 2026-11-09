using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IAcademicSessionService
{
    Task<Result<AcademicSessionListDto>> ListAsync(CancellationToken ct = default);

    Task<Result<AcademicSessionDto>> CreateAsync(SaveAcademicSessionRequest request, CancellationToken ct = default);

    Task<Result<AcademicSessionDto>> UpdateAsync(int id, SaveAcademicSessionRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    Task<Result> SetCurrentAsync(int id, CancellationToken ct = default);
}
