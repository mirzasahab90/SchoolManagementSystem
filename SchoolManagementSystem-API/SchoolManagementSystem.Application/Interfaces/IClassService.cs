using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IClassService
{
    Task<Result<IReadOnlyList<ClassDto>>> ListAsync(CancellationToken ct = default);

    Task<Result<ClassDetailDto>> GetAsync(int id, CancellationToken ct = default);

    Task<Result<ClassDetailDto>> CreateAsync(SaveClassRequest request, CancellationToken ct = default);

    Task<Result<ClassDetailDto>> UpdateAsync(int id, SaveClassRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Replaces the class's subject list, in the given order.</summary>
    Task<Result<ClassDetailDto>> SetSubjectsAsync(int id, IReadOnlyList<SaveClassSubjectItem> items, CancellationToken ct = default);
}
