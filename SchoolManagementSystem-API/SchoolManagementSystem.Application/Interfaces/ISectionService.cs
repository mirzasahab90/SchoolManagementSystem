using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Application.Interfaces;

public interface ISectionService
{
    /// <summary>Sections of the request's campuses; <paramref name="sessionId"/> defaults to the effective current session.</summary>
    Task<Result<IReadOnlyList<SectionDto>>> ListAsync(int? sessionId, int? classId, CancellationToken ct = default);

    Task<Result<SectionDto>> CreateAsync(SaveSectionRequest request, CancellationToken ct = default);

    Task<Result<SectionDto>> UpdateAsync(int id, SaveSectionRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    Task<Result<CopySectionsResultDto>> CopyAsync(CopySectionsRequest request, CancellationToken ct = default);
}
