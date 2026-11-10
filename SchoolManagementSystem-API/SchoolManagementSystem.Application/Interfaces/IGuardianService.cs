using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Students;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IGuardianService
{
    Task<Result<PagedResult<GuardianListItemDto>>> SearchAsync(string? q, int page, int pageSize, CancellationToken ct = default);

    Task<Result<GuardianDetailDto>> GetAsync(int id, CancellationToken ct = default);

    Task<Result<GuardianDetailDto>> UpdateAsync(int id, SaveGuardianRequest request, CancellationToken ct = default);

    /// <summary>Exact CNIC/phone match for the admission form: id, name and child count only.</summary>
    Task<Result<IReadOnlyList<GuardianLookupDto>>> LookupAsync(string? cnic, string? phone, CancellationToken ct = default);
}
