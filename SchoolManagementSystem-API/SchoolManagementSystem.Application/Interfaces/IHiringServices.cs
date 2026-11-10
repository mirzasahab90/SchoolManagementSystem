using Microsoft.AspNetCore.Http;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Academics;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IVacancyService
{
    Task<Result<IReadOnlyList<VacancyDto>>> ListAsync(VacancyStatus? status, CancellationToken ct = default);
    Task<Result<VacancyDto>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<VacancyDto>> CreateAsync(SaveVacancyRequest request, CancellationToken ct = default);
    Task<Result<VacancyDto>> UpdateAsync(int id, SaveVacancyRequest request, CancellationToken ct = default);

    /// <summary>Open / OnHold / Closed by hand; Filled is set by hiring.</summary>
    Task<Result<VacancyDto>> SetStatusAsync(int id, VacancyStatusRequest request, CancellationToken ct = default);

    /// <summary>Only a vacancy without candidates.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface ICandidateService
{
    /// <summary>
    /// Paged list, newest first. <paramref name="stage"/>: a stage name, "active" or "closed"; total covers the whole filter,
    /// so one stage plus page / pageSize pages a single board column. Applied range is inclusive, on the UTC created date.
    /// </summary>
    Task<Result<PagedResult<CandidateListItemDto>>> SearchAsync(
        int? vacancyId, string? stage, string? q, CandidateSource? source, DateOnly? appliedFrom, DateOnly? appliedTo,
        int page, int pageSize, CancellationToken ct = default);
    Task<Result<CandidateProfileDto>> GetProfileAsync(int id, CancellationToken ct = default);
    Task<Result<CandidateProfileDto>> CreateAsync(SaveCandidateRequest request, CancellationToken ct = default);
    Task<Result<CandidateProfileDto>> UpdateAsync(int id, SaveCandidateRequest request, CancellationToken ct = default);
    Task<Result<CandidateProfileDto>> MoveAsync(int id, MoveCandidateRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result> SetCvAsync(int id, IFormFile file, CancellationToken ct = default);
    Task<Result<(Stream Content, string ContentType, string FileName)>> GetCvAsync(int id, CancellationToken ct = default);

    Task<Result<CandidateProfileDto>> ScheduleInterviewAsync(int id, ScheduleInterviewRequest request, CancellationToken ct = default);
    Task<Result<CandidateProfileDto>> UpdateInterviewAsync(int id, int roundId, UpdateInterviewRequest request, CancellationToken ct = default);
    Task<Result<CandidateProfileDto>> DeleteInterviewAsync(int id, int roundId, CancellationToken ct = default);
    Task<Result<CandidateProfileDto>> MakeOfferAsync(int id, MakeOfferRequest request, CancellationToken ct = default);

    /// <summary>Accepted, Declined or Withdrawn; only for a pending offer.</summary>
    Task<Result<CandidateProfileDto>> RespondOfferAsync(int id, OfferStatus response, CancellationToken ct = default);

    Task<Result<StaffDetailDto>> HireAsync(int id, HireRequest request, CancellationToken ct = default);
}

public interface IStaffService
{
    /// <summary><paramref name="sort"/>: name, employeeNo, designation, joined (unknown = default name order); <paramref name="dir"/>: asc | desc.</summary>
    Task<Result<PagedResult<StaffListItemDto>>> SearchAsync(
        string? q, StaffStatus? status, StaffCategory? category, StaffDesignation? designation, string? sort, string? dir,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>Distinct designations in use among the staff the caller can see, alphabetical.</summary>
    Task<Result<IReadOnlyList<StaffDesignation>>> DesignationsAsync(CancellationToken ct = default);
    Task<Result<StaffDetailDto>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<StaffProfileDto>> GetProfileAsync(int id, CancellationToken ct = default);
    Task<Result<StaffBankDto>> GetBankAsync(int id, CancellationToken ct = default);

    /// <summary>Direct onboarding (no candidate); needs a working campus.</summary>
    Task<Result<StaffProfileDto>> CreateAsync(SaveStaffRequest request, CancellationToken ct = default);
    Task<Result<StaffProfileDto>> UpdateAsync(int id, SaveStaffRequest request, CancellationToken ct = default);

    /// <summary>Only directly added staff still onboarding, without a login or assignments.</summary>
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);

    Task<Result> SetPhotoAsync(int id, IFormFile file, CancellationToken ct = default);
    Task<Result<(Stream Content, string ContentType)>> GetPhotoAsync(int id, CancellationToken ct = default);
    Task<Result<StaffDocumentDto>> UploadDocumentAsync(int id, StaffDocumentType type, IFormFile file, CancellationToken ct = default);
    Task<Result<(Stream Content, string ContentType, string FileName)>> GetDocumentAsync(int id, int docId, CancellationToken ct = default);
    Task<Result> DeleteDocumentAsync(int id, int docId, CancellationToken ct = default);

    /// <summary>"contract" or "orientation"; completing all four items activates an onboarding member.</summary>
    Task<Result<StaffProfileDto>> SetChecklistAsync(int id, string item, bool done, CancellationToken ct = default);

    Task<Result<StaffProfileDto>> CreateLoginAsync(int id, CreateStaffLoginRequest request, CancellationToken ct = default);
    Task<Result<StaffProfileDto>> LinkLoginAsync(int id, LinkStaffLoginRequest request, CancellationToken ct = default);

    /// <summary>Current-session sections of the member's campus with class subjects and their current teachers.</summary>
    Task<Result<AssignmentOptionsDto>> GetAssignmentOptionsAsync(int id, CancellationToken ct = default);
    Task<Result<StaffProfileDto>> AssignAsync(int id, AssignRequest request, CancellationToken ct = default);
    Task<Result<StaffProfileDto>> EndAssignmentAsync(int id, int assignmentId, CancellationToken ct = default);
    Task<Result<SectionDto>> SetClassTeacherAsync(int sectionId, ClassTeacherRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<OptionSubjectDto>>> GetSectionTeachersAsync(int sectionId, CancellationToken ct = default);

    /// <summary>Teaching staff of the section's campus who have not left.</summary>
    Task<Result<IReadOnlyList<ClassTeacherOptionDto>>> GetClassTeacherOptionsAsync(int sectionId, CancellationToken ct = default);

    /// <summary>OnLeave / Active / Resigned / Terminated; leaving needs a date and reason and closes login and classes.</summary>
    Task<Result<StaffProfileDto>> SetStatusAsync(int id, StaffStatusRequest request, CancellationToken ct = default);
}
