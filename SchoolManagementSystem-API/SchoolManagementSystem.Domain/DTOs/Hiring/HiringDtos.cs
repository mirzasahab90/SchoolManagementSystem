using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Domain.DTOs.Hiring;

public record StageCountsDto(int Applied, int Shortlisted, int Interview, int Offered, int Hired);

public record VacancyDto(
    int Id, int CampusId, string CampusCode, string Title, StaffDesignation Designation, StaffCategory Category,
    int Positions, int Hired, IReadOnlyList<int> ClassIds, IReadOnlyList<int> SubjectIds, string? Requirements,
    DateOnly? ClosingDate, VacancyStatus Status, StageCountsDto Counts);

public record SaveVacancyRequest(
    string Title, StaffDesignation Designation, StaffCategory Category, int Positions, IReadOnlyList<int>? ClassIds,
    IReadOnlyList<int>? SubjectIds, string? Requirements, DateOnly? ClosingDate);

public record VacancyStatusRequest(VacancyStatus Status);

public record CandidateListItemDto(
    int Id, int VacancyId, string VacancyTitle, string FullName, string Phone, string? Qualification,
    decimal? ExperienceYears, CandidateStage Stage, int? LatestRating, bool HasCv, string CampusCode);

public record SaveCandidateRequest(
    int VacancyId, string FullName, string? Cnic, string Phone, string? Email, string? Qualification,
    decimal? ExperienceYears, decimal? CurrentSalary, decimal? ExpectedSalary, CandidateSource Source);

public record MoveCandidateRequest(CandidateStage Stage, string? Reason);

public record InterviewRoundDto(
    int Id, string Name, DateTime ScheduledAt, int InterviewerUserId, string InterviewerName, InterviewResult Result,
    int? Rating, string? Notes);

public record ScheduleInterviewRequest(string Name, DateTime ScheduledAt, int InterviewerUserId);

public record UpdateInterviewRequest(
    string Name, DateTime ScheduledAt, int InterviewerUserId, InterviewResult Result, int? Rating, string? Notes);

public record OfferDto(
    int Id, StaffDesignation Designation, EmploymentType EmploymentType, decimal OfferedSalary,
    DateOnly ProposedJoiningDate, OfferStatus Status, DateTime? RespondedOn);

public record MakeOfferRequest(StaffDesignation Designation, EmploymentType EmploymentType, decimal OfferedSalary, DateOnly ProposedJoiningDate);

public record StageHistoryDto(CandidateStage? FromStage, CandidateStage ToStage, string? Reason, DateTime OccurredOn, string? ByUserName);

public record CandidateDetailDto(
    int Id, int CampusId, string CampusCode, int VacancyId, string VacancyTitle, string FullName, string? Cnic,
    string Phone, string? Email, string? Qualification, decimal? ExperienceYears, decimal? CurrentSalary,
    decimal? ExpectedSalary, CandidateSource Source, CandidateStage Stage, string? StageReason, bool HasCv,
    string? CvFileName, int? StaffId, string? ExistingStaffWarning);

public record CandidateProfileDto(
    CandidateDetailDto Candidate, IReadOnlyList<InterviewRoundDto> Interviews, OfferDto? Offer,
    IReadOnlyList<StageHistoryDto> History, IReadOnlyList<CandidateStage> AllowedMoves);

public record HireRequest(DateOnly JoiningDate, EmploymentType EmploymentType, decimal BasicSalary);

public record StaffListItemDto(
    int Id, string EmployeeNo, string FullName, StaffDesignation Designation, StaffCategory Category,
    DateOnly JoiningDate, StaffStatus Status, string CampusCode);

public record StaffDetailDto(
    int Id, int CampusId, string CampusCode, string EmployeeNo, string FirstName, string LastName, string FullName,
    string? Cnic, string Phone, string? Email, StaffCategory Category, StaffDesignation Designation,
    EmploymentType EmploymentType, DateOnly JoiningDate, decimal BasicSalary, StaffStatus Status, int? SourceCandidateId);

public record UserPickDto(int Id, string FullName);
