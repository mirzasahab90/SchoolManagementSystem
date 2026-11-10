using SchoolManagementSystem.Domain.Entities.Exams;

namespace SchoolManagementSystem.Domain.DTOs.Exams;

// ---------------------------------------------------------------- grading

public record GradeBandDto(string Grade, decimal MinPercent, bool IsFail);

public record GradeScaleDto(int Id, int? CampusId, string Name, IReadOnlyList<GradeBandDto> Bands);

public record SaveScaleRequest(string Name, IReadOnlyList<GradeBandDto> Bands, bool MainCampus);

public record SchemeDto(
    int? Id, int? CampusId, int? SessionId, int? ClassId, int MonthlyWeight, int MidTermWeight, int FinalWeight, int OtherWeight,
    bool MustPassFinal);

public record SchemesDto(SchemeDto Effective, IReadOnlyList<SchemeDto> Owned);

public record SaveSchemeRequest(
    int? SessionId, int? ClassId, int MonthlyWeight, int MidTermWeight, int FinalWeight, int OtherWeight, bool MustPassFinal, bool MainCampus);

// ---------------------------------------------------------------- exams and papers

public record ExamDto(
    int Id, int? CampusId, string? CampusCode, int SessionId, string SessionName, string Name, ExamType Type, DateOnly? StartDate,
    DateOnly? EndDate, ExamStatus Status, int SectionsTotal, int SectionsSubmitted, int SectionsPublished);

public record PaperDto(
    int Id, int SubjectId, string SubjectName, decimal TotalMarks, decimal PassMarks, bool HasPractical, decimal? PracticalMarks,
    DateOnly? PaperDate, bool IsActive);

public record SectionProgressDto(int SectionId, string SectionName, int Submitted, int Papers, bool Published);

public record ExamClassDto(int ClassId, string ClassName, IReadOnlyList<PaperDto> Papers, IReadOnlyList<SectionProgressDto> Sections);

public record ExamDetailDto(ExamDto Exam, IReadOnlyList<ExamClassDto> Classes);

public record SaveExamRequest(string Name, ExamType Type, int SessionId, DateOnly? StartDate, DateOnly? EndDate, IReadOnlyList<int>? ClassIds, bool MainCampus = false);

public record ExamStatusRequest(ExamStatus Status);

public record AddClassesRequest(IReadOnlyList<int> ClassIds);

public record SavePaperRequest(decimal TotalMarks, decimal PassMarks, bool HasPractical, decimal? PracticalMarks, DateOnly? PaperDate, bool IsActive);

// ---------------------------------------------------------------- marks

public record MyPaperDto(
    int ExamId, string ExamName, int PaperId, int SectionId, string ClassName, string SectionName, string SubjectName,
    PaperSectionStatus Status, bool Published);

public record MarkRowDto(
    int StudentId, int RollNo, string Name, decimal? Theory, decimal? Practical, MarkStatus? Status, decimal? Obtained, string? Grade);

public record MarkSheetDto(
    PaperDto Paper, int ExamId, string ExamName, ExamStatus ExamStatus, int SectionId, string ClassName, string SectionName,
    PaperSectionStatus Status, bool Published, bool CanEdit, bool CanReturn, string? LockedReason, IReadOnlyList<GradeBandDto> Bands,
    IReadOnlyList<MarkRowDto> Rows);

public record MarkRowRequest(int StudentId, decimal? Theory, decimal? Practical, MarkStatus Status);

public record SaveMarksRequest(IReadOnlyList<MarkRowRequest> Rows);

// ---------------------------------------------------------------- results

public record SubjectCalc(
    int SubjectId, string Name, decimal? Theory, decimal? Practical, decimal? Obtained, decimal Total, decimal? Percent, string? Grade,
    bool? Passed, MarkStatus? Status);

public record TabRowDto(
    int StudentId, int RollNo, string Name, IReadOnlyList<SubjectCalc> Subjects, decimal? Obtained, decimal? Total, decimal? Percent,
    string? Grade, bool? Passed, int? Position);

public record TabulationDto(
    int ExamId, string ExamName, int SectionId, string ClassName, string SectionName, bool Published, DateTime? PublishedOn,
    IReadOnlyList<string> Subjects, IReadOnlyList<TabRowDto> Rows, IReadOnlyList<string> PendingSubjects);

public record SaveRemarkRequest(int? ExamId, int SessionId, int StudentId, string Remark);

public record AnnualCalc(
    decimal? Percent, string? Grade, bool? Passed, IReadOnlyDictionary<ExamType, decimal?> TypePercents,
    IReadOnlyDictionary<ExamType, decimal> EffectiveWeights);

public record ExamPercentDto(int ExamId, string Name, ExamType Type, decimal? Percent);

public record AnnualRowDto(int StudentId, int RollNo, string Name, AnnualCalc Result, IReadOnlyList<ExamPercentDto> Exams);

// ---------------------------------------------------------------- cards

public record StudentCardInfo(int StudentId, string Name, string AdmissionNo, string ClassName, string SectionName, int RollNo, bool HasPhoto);

public record ReportCardDto(
    string SchoolName, string CampusName, string Title, string SessionName, StudentCardInfo Student, IReadOnlyList<SubjectCalc> Subjects,
    decimal? Obtained, decimal? Total, decimal? Percent, string? Grade, bool? Passed, int? Position, decimal? AttendancePct,
    string? Remark, AnnualCalc? Annual, IReadOnlyList<ExamPercentDto>? Exams);

public record StudentResultsDto(IReadOnlyList<StudentExamResultDto> Exams, AnnualCalc? Annual);

public record StudentExamResultDto(int ExamId, string Name, ExamType Type, decimal? Percent, string? Grade, bool? Passed, int? Position);
