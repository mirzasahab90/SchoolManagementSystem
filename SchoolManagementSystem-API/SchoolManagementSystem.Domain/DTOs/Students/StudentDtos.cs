using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Domain.DTOs.Students;

public record PagedResult<T>(IReadOnlyList<T> Items, int Total);

public record StudentListItemDto(
    int Id, string AdmissionNo, string FullName, Gender Gender, bool HasPhoto, string? ClassName, string? SectionName,
    int? RollNo, string? GuardianName, string? GuardianPhone, StudentStatus Status, string CampusCode);

public record StudentDetailDto(
    int Id, int CampusId, string CampusCode, string AdmissionNo, string FirstName, string LastName, string FullName,
    Gender Gender, DateOnly DateOfBirth, string? BFormNo, string? BloodGroup, string? Religion, string? Nationality,
    string? Address, string? Phone, DateOnly AdmissionDate, string? PreviousSchool, string? Notes, bool HasPhoto,
    StudentStatus Status, DateOnly? LeftOn, string? LeaveReason);

public record SaveStudentRequest(
    string FirstName, string LastName, Gender Gender, DateOnly DateOfBirth, string? BFormNo, string? BloodGroup,
    string? Religion, string? Nationality, string? Address, string? Phone, DateOnly AdmissionDate,
    string? PreviousSchool, string? Notes);

public record SaveGuardianRequest(
    string FullName, string? Cnic, string Phone, string? AltPhone, string? Email, string? Occupation, string? Address);

/// <summary>Either an existing guardian (<see cref="GuardianId"/>) or a new one (<see cref="NewGuardian"/>).</summary>
public record GuardianLinkRequest(
    int? GuardianId, SaveGuardianRequest? NewGuardian, GuardianRelation Relation, bool IsPrimary, bool IsEmergencyContact);

public record AdmissionEnrollmentRequest(int? SessionId, int SectionId, int? RollNo);

public record AdmissionRequest(
    SaveStudentRequest Student, string? AdmissionNo, AdmissionEnrollmentRequest Enrollment, IReadOnlyList<GuardianLinkRequest> Guardians);

public record StudentGuardianDto(
    int GuardianId, string FullName, string? Cnic, string Phone, string? AltPhone, string? Email, string? Occupation,
    GuardianRelation Relation, bool IsPrimary, bool IsEmergencyContact);

public record EnrollmentDto(
    int Id, int SessionId, string SessionName, int ClassId, string ClassName, int SectionId, string SectionName,
    int RollNo, EnrollmentStatus Status, DateOnly StartDate, DateOnly? EndDate);

public record EnrollmentHistoryDto(
    int Id, EnrollmentAction Action, string? FromSection, string? ToSection, string? Reason, DateTime OccurredOn, string? ByUserName);

public record StudentDocumentDto(int Id, DocumentType Type, string FileName, string ContentType, long SizeBytes, DateTime UploadedOn);

/// <summary>StudentId is null when the sibling is on a campus the caller cannot access.</summary>
public record SiblingDto(int? StudentId, string FullName, string CampusCode, string? ClassName, string? SectionName);

public record StudentProfileDto(
    StudentDetailDto Student, IReadOnlyList<StudentGuardianDto> Guardians, EnrollmentDto? Enrollment,
    IReadOnlyList<EnrollmentHistoryDto> History, IReadOnlyList<StudentDocumentDto> Documents, IReadOnlyList<SiblingDto> Siblings);

public record UpdateGuardianLinkRequest(bool IsPrimary, bool IsEmergencyContact);

public record ChangeSectionRequest(int SectionId, int? RollNo, string? Reason);

public record LeaveRequest(DateOnly LeftOn, string Reason);

public record RejoinRequest(int? SectionId, int? RollNo);

public record GuardianListItemDto(int Id, string FullName, string? Cnic, string Phone, int ChildrenCount);

public record GuardianDetailDto(
    int Id, string FullName, string? Cnic, string Phone, string? AltPhone, string? Email, string? Occupation, string? Address,
    IReadOnlyList<SiblingDto> Children);

public record GuardianLookupDto(int Id, string FullName, int ChildrenCount);
