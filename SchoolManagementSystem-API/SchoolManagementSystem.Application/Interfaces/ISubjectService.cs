using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Application.Interfaces;

public interface ISubjectService
{
    Task<Result<IReadOnlyList<SubjectDto>>> ListAsync(CancellationToken ct = default);

    Task<Result<SubjectDto>> CreateAsync(SaveSubjectRequest request, CancellationToken ct = default);

    Task<Result<SubjectDto>> UpdateAsync(int id, SaveSubjectRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
