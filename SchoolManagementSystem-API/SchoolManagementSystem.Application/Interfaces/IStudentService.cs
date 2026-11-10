using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IStudentService
{
    /// <summary>
    /// Paged list. <paramref name="sessionId"/>: students with any enrollment in that session; admitted range is inclusive;
    /// <paramref name="sort"/>: name, admissionNo, admitted, class, roll (unknown = default name order); <paramref name="dir"/>: asc | desc.
    /// </summary>
    Task<Result<PagedResult<StudentListItemDto>>> SearchAsync(
        string? q, int? classId, int? sectionId, StudentStatus? status, int? sessionId, DateOnly? admittedFrom, DateOnly? admittedTo,
        string? sort, string? dir, int page, int pageSize, CancellationToken ct = default);

    Task<Result<StudentProfileDto>> GetProfileAsync(int id, CancellationToken ct = default);

    /// <summary>One-step admission: student, guardians and enrollment in one transaction.</summary>
    Task<Result<StudentProfileDto>> AdmitAsync(AdmissionRequest request, CancellationToken ct = default);

    Task<Result<StudentProfileDto>> UpdateAsync(int id, SaveStudentRequest request, CancellationToken ct = default);

    /// <summary>Links a guardian; a new primary demotes the old one.</summary>
    Task<Result<StudentProfileDto>> AddGuardianAsync(int studentId, GuardianLinkRequest request, CancellationToken ct = default);

    Task<Result<StudentProfileDto>> RemoveGuardianAsync(int studentId, int guardianId, CancellationToken ct = default);

    /// <summary>Changes primary / emergency flags of a linked guardian; making one primary demotes the others.</summary>
    Task<Result<StudentProfileDto>> UpdateGuardianLinkAsync(int studentId, int guardianId, UpdateGuardianLinkRequest request, CancellationToken ct = default);

    Task<Result<StudentProfileDto>> ChangeSectionAsync(int id, ChangeSectionRequest request, CancellationToken ct = default);

    Task<Result<StudentProfileDto>> LeaveAsync(int id, LeaveRequest request, CancellationToken ct = default);

    Task<Result<StudentProfileDto>> RejoinAsync(int id, RejoinRequest request, CancellationToken ct = default);

    /// <summary>Soft-deletes the student and their enrollments and documents. Files stay on disk.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}
