using SchoolManagementSystem.Domain.Entities.Hiring;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Domain.DTOs.Hiring;

/// <summary>Direct onboarding and edit. On edit a null BankAccount keeps the stored value and "" clears it.</summary>
public record SaveStaffRequest(
    string FirstName, string LastName, string? Cnic, string Phone, string? Email, Gender? Gender, DateOnly? DateOfBirth,
    string? Address, string? EmergencyName, string? EmergencyRelation, string? EmergencyPhone, StaffCategory Category,
    StaffDesignation Designation, EmploymentType EmploymentType, DateOnly JoiningDate, decimal BasicSalary,
    string? Qualification, decimal? ExperienceYears, string? PreviousEmployer, string? BankName, string? BankAccount);

public record StaffPersonalDto(
    Gender? Gender, DateOnly? DateOfBirth, string? Address, bool HasPhoto, string? EmergencyName, string? EmergencyRelation,
    string? EmergencyPhone, string? Qualification, decimal? ExperienceYears, string? PreviousEmployer, string? BankName,
    string? BankAccountMasked, DateOnly? LeftOn, string? LeaveReason);

public record StaffBankDto(string? BankName, string? BankAccount);

public record StaffDocumentDto(int Id, StaffDocumentType Type, string FileName, string ContentType, long SizeBytes, DateTime UploadedOn);

public record ChecklistDto(
    bool DocumentsReceived, IReadOnlyList<StaffDocumentType> MissingDocuments, bool ContractSigned, DateTime? ContractSignedOn,
    bool LoginCreated, bool OrientationDone, DateTime? OrientationDoneOn, int DoneCount);

public record StaffAccountDto(int UserId, string Email, IReadOnlyList<string> Roles, bool IsActive, bool MustChangePassword);

public record AssignmentDto(int Id, int SectionId, string ClassName, string SectionName, int SubjectId, string SubjectName, DateOnly StartedOn);

public record ClassTeacherDto(int SectionId, string ClassName, string SectionName);

public record StaffProfileDto(
    StaffDetailDto Staff, StaffPersonalDto Personal, ChecklistDto Checklist, IReadOnlyList<StaffDocumentDto> Documents,
    IReadOnlyList<AssignmentDto> Assignments, IReadOnlyList<ClassTeacherDto> ClassTeacherOf, StaffAccountDto? Account,
    IReadOnlyList<StaffStatus> AllowedStatuses);

public record CreateStaffLoginRequest(string Email, string Password, int RoleId);

public record LinkStaffLoginRequest(int UserId);

public record ChecklistRequest(bool Done);

public record StaffStatusRequest(StaffStatus Status, DateOnly? Date, string? Reason);

public record AssignRequest(int SectionId, IReadOnlyList<int> SubjectIds);

public record ClassTeacherRequest(int? StaffId);

public record ClassTeacherOptionDto(int Id, string FullName, string EmployeeNo);

public record OptionSubjectDto(int SubjectId, string SubjectName, int? TeacherStaffId, string? TeacherName);

public record OptionSectionDto(int SectionId, string ClassName, string SectionName, IReadOnlyList<OptionSubjectDto> Subjects);

public record AssignmentOptionsDto(int SessionId, string SessionName, IReadOnlyList<OptionSectionDto> Sections);
