using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Exams;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IGradingService
{
    /// <summary>The scale the working campus uses (its own, else Main Campus).</summary>
    Task<Result<GradeScaleDto>> GetScaleAsync(CancellationToken ct = default);
    Task<Result<GradeScaleDto>> SaveScaleAsync(SaveScaleRequest request, CancellationToken ct = default);
    Task<Result> DeleteCampusScaleAsync(CancellationToken ct = default);
    Task<Result<SchemesDto>> GetSchemesAsync(int? sessionId, int? classId, CancellationToken ct = default);
    Task<Result<SchemeDto>> SaveSchemeAsync(SaveSchemeRequest request, CancellationToken ct = default);
    Task<Result> DeleteSchemeAsync(int id, CancellationToken ct = default);
}

public interface IExamService
{
    Task<Result<IReadOnlyList<ExamDto>>> ListAsync(int? sessionId, CancellationToken ct = default);
    Task<Result<ExamDetailDto>> GetAsync(int id, CancellationToken ct = default);

    /// <summary>Creates the exam with one paper per subject of each picked class (100/33, Monthly 25/9).</summary>
    Task<Result<ExamDetailDto>> CreateAsync(SaveExamRequest request, CancellationToken ct = default);
    Task<Result<ExamDetailDto>> UpdateAsync(int id, SaveExamRequest request, CancellationToken ct = default);
    Task<Result<ExamDto>> SetStatusAsync(int id, Domain.Entities.Exams.ExamStatus status, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<PaperDto>> UpdatePaperAsync(int examId, int paperId, SavePaperRequest request, CancellationToken ct = default);
    Task<Result<ExamDetailDto>> AddClassesAsync(int examId, IReadOnlyList<int> classIds, CancellationToken ct = default);
}

public interface IResultService
{
    /// <summary>The stored snapshot when the section is published, else computed live.</summary>
    Task<Result<TabulationDto>> TabulateAsync(int examId, int sectionId, CancellationToken ct = default);
    Task<Result<(byte[] Content, string FileName)>> CsvAsync(int examId, int sectionId, CancellationToken ct = default);
    Task<Result<TabulationDto>> PublishAsync(int examId, int sectionId, CancellationToken ct = default);
    Task<Result<TabulationDto>> UnpublishAsync(int examId, int sectionId, CancellationToken ct = default);
    Task<Result> SaveRemarkAsync(SaveRemarkRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AnnualRowDto>>> AnnualAsync(int sessionId, int sectionId, CancellationToken ct = default);
}

public interface IReportCardService
{
    /// <summary>Exam cards of a published section (or one student's).</summary>
    Task<Result<IReadOnlyList<ReportCardDto>>> ExamCardsAsync(int examId, int? sectionId, int? studentId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ReportCardDto>>> AnnualCardsAsync(int sessionId, int? sectionId, int? studentId, CancellationToken ct = default);
    Task<Result<StudentResultsDto>> StudentResultsAsync(int studentId, CancellationToken ct = default);
}

public interface IMarksService
{
    /// <summary>Papers × sections the caller can enter: all for admins, the assigned (section, subject) papers for teachers.</summary>
    Task<Result<IReadOnlyList<MyPaperDto>>> MyPapersAsync(int? examId, CancellationToken ct = default);
    Task<Result<MarkSheetDto>> GetAsync(int paperId, int sectionId, CancellationToken ct = default);
    Task<Result<MarkSheetDto>> SaveAsync(int paperId, int sectionId, SaveMarksRequest request, CancellationToken ct = default);
    Task<Result<MarkSheetDto>> SubmitAsync(int paperId, int sectionId, CancellationToken ct = default);
    Task<Result<MarkSheetDto>> ReturnAsync(int paperId, int sectionId, CancellationToken ct = default);
}
