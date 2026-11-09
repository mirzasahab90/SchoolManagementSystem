using SchoolManagementSystem.Domain.Entities.Academics;

namespace SchoolManagementSystem.Domain.DTOs.Academics;

// Owner fields: CampusId / CampusCode are null for head-office (HQ) rows.

public record AcademicSessionDto(
    int Id, string Name, DateOnly StartDate, DateOnly EndDate, SessionStatus Status, bool IsCurrent,
    int? CampusId, string? CampusCode, int SectionCount);

public record AcademicSessionListDto(IReadOnlyList<AcademicSessionDto> Items, int? EffectiveCurrentId);

public record SaveAcademicSessionRequest(
    string Name, DateOnly StartDate, DateOnly EndDate, SessionStatus Status, int? CampusId = null);

public record SubjectDto(
    int Id, string Code, string Name, SubjectType Type, bool IsActive, int? CampusId, string? CampusCode, int ClassCount);

public record SaveSubjectRequest(string Code, string Name, SubjectType Type, bool IsActive = true, int? CampusId = null);

public record ClassDto(
    int Id, string Code, string Name, ClassStage Stage, int DisplayOrder, bool IsActive,
    int? CampusId, string? CampusCode, int SubjectCount, int SectionCount);

public record ClassSubjectDto(int SubjectId, string Code, string Name, SubjectType Type, bool IsOptional, int DisplayOrder);

public record ClassDetailDto(ClassDto Class, IReadOnlyList<ClassSubjectDto> Subjects);

public record SaveClassRequest(
    string Code, string Name, ClassStage Stage, int DisplayOrder, bool IsActive = true, int? CampusId = null);

public record SaveClassSubjectItem(int SubjectId, bool IsOptional);

public record SectionDto(
    int Id, int CampusId, string CampusCode, string CampusName, int AcademicSessionId, string SessionName,
    int ClassId, string ClassCode, string ClassName, int ClassOrder, string Name, SectionShift Shift,
    int? Capacity, string? Room, bool IsActive);

public record SaveSectionRequest(
    int ClassId, int? AcademicSessionId, string Name, SectionShift Shift, int? Capacity, string? Room,
    bool IsActive = true, int? CampusId = null);

public record CopySectionsRequest(int FromSessionId, int ToSessionId, bool DryRun);

public record CopySectionsResultDto(int Copied, int Skipped);
