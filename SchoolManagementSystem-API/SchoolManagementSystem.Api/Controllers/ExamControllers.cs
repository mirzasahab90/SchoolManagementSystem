using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Exams;

namespace SchoolManagementSystem.Api.Controllers;

public class ExamsController(IExamService service) : ApiControllerBase
{
    [HttpGet]
    [HasAnyPermission("exams:View", "marks:View", "results:View")]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<ExamDto>>>> List([FromQuery] int? sessionId, CancellationToken ct) =>
        FromResult(await service.ListAsync(sessionId, ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission("exams:View", "marks:View", "results:View")]
    public async Task<ActionResult<ResponseDto<ExamDetailDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Exams, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<ExamDetailDto>>> Create([FromBody] SaveExamRequest request, CancellationToken ct) =>
        FromResult(await service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Exams, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<ExamDetailDto>>> Update(int id, [FromBody] SaveExamRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/status")]
    [HasPermission(AppModules.Exams, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<ExamDto>>> Status(int id, [FromBody] ExamStatusRequest request, CancellationToken ct) =>
        FromResult(await service.SetStatusAsync(id, request.Status, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Exams, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPut("{id:int}/papers/{paperId:int}")]
    [HasPermission(AppModules.Exams, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<PaperDto>>> UpdatePaper(int id, int paperId, [FromBody] SavePaperRequest request, CancellationToken ct) =>
        FromResult(await service.UpdatePaperAsync(id, paperId, request, ct));

    [HttpPost("{id:int}/classes")]
    [HasPermission(AppModules.Exams, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<ExamDetailDto>>> AddClasses(int id, [FromBody] AddClassesRequest request, CancellationToken ct) =>
        FromResult(await service.AddClassesAsync(id, request.ClassIds, ct));
}

public class MarksController(IMarksService service) : ApiControllerBase
{
    [HttpGet("my-papers")]
    [HasPermission(AppModules.Marks, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<MyPaperDto>>>> MyPapers([FromQuery] int? examId, CancellationToken ct) =>
        FromResult(await service.MyPapersAsync(examId, ct));

    [HttpGet("{paperId:int}/sections/{sectionId:int}")]
    [HasPermission(AppModules.Marks, AppActions.View)]
    public async Task<ActionResult<ResponseDto<MarkSheetDto>>> Get(int paperId, int sectionId, CancellationToken ct) =>
        FromResult(await service.GetAsync(paperId, sectionId, ct));

    [HttpPut("{paperId:int}/sections/{sectionId:int}")]
    [HasPermission(AppModules.Marks, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<MarkSheetDto>>> Save(int paperId, int sectionId, [FromBody] SaveMarksRequest request, CancellationToken ct) =>
        FromResult(await service.SaveAsync(paperId, sectionId, request, ct));

    [HttpPost("{paperId:int}/sections/{sectionId:int}/submit")]
    [HasPermission(AppModules.Marks, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<MarkSheetDto>>> Submit(int paperId, int sectionId, CancellationToken ct) =>
        FromResult(await service.SubmitAsync(paperId, sectionId, ct));

    [HttpPost("{paperId:int}/sections/{sectionId:int}/return")]
    [HasPermission(AppModules.Marks, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<MarkSheetDto>>> Return(int paperId, int sectionId, CancellationToken ct) =>
        FromResult(await service.ReturnAsync(paperId, sectionId, ct));
}

public class ResultsController(IResultService service) : ApiControllerBase
{
    [HttpGet("{examId:int}/sections/{sectionId:int}")]
    [HasPermission(AppModules.Results, AppActions.View)]
    public async Task<ActionResult<ResponseDto<TabulationDto>>> Get(int examId, int sectionId, CancellationToken ct) =>
        FromResult(await service.TabulateAsync(examId, sectionId, ct));

    [HttpGet("{examId:int}/sections/{sectionId:int}/csv")]
    [HasPermission(AppModules.Results, AppActions.View)]
    public async Task<IActionResult> Csv(int examId, int sectionId, CancellationToken ct)
    {
        var result = await service.CsvAsync(examId, sectionId, ct);
        return result.IsSuccess ? File(result.Value.Content, "text/csv; charset=utf-8", result.Value.FileName) : Failure<object>(result.Error!);
    }

    [HttpPost("{examId:int}/sections/{sectionId:int}/publish")]
    [HasPermission(AppModules.Results, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<TabulationDto>>> Publish(int examId, int sectionId, CancellationToken ct) =>
        FromResult(await service.PublishAsync(examId, sectionId, ct));

    [HttpPost("{examId:int}/sections/{sectionId:int}/unpublish")]
    [HasPermission(AppModules.Results, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<TabulationDto>>> Unpublish(int examId, int sectionId, CancellationToken ct) =>
        FromResult(await service.UnpublishAsync(examId, sectionId, ct));

    [HttpGet("annual")]
    [HasPermission(AppModules.Results, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<AnnualRowDto>>>> Annual([FromQuery] int sessionId, [FromQuery] int sectionId, CancellationToken ct) =>
        FromResult(await service.AnnualAsync(sessionId, sectionId, ct));

    [HttpPut("remarks")]
    [HasPermission(AppModules.Results, AppActions.View)]
    public async Task<ActionResult<ResponseDto<object>>> Remark([FromBody] SaveRemarkRequest request, CancellationToken ct) =>
        FromResult(await service.SaveRemarkAsync(request, ct));
}

[Route("api/report-cards")]
public class ReportCardsController(IReportCardService service) : ApiControllerBase
{
    [HttpGet("exam/{examId:int}")]
    [HasPermission(AppModules.Results, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<ReportCardDto>>>> Exam(
        int examId, [FromQuery] int? sectionId, [FromQuery] int? studentId, CancellationToken ct) =>
        FromResult(await service.ExamCardsAsync(examId, sectionId, studentId, ct));

    [HttpGet("annual")]
    [HasPermission(AppModules.Results, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<ReportCardDto>>>> Annual(
        [FromQuery] int sessionId, [FromQuery] int? sectionId, [FromQuery] int? studentId, CancellationToken ct) =>
        FromResult(await service.AnnualCardsAsync(sessionId, sectionId, studentId, ct));
}

public class GradingController(IGradingService service) : ApiControllerBase
{
    [HttpGet("scale")]
    [HasAnyPermission("grading:View", "results:View", "exams:View", "marks:View")]
    public async Task<ActionResult<ResponseDto<GradeScaleDto>>> Scale(CancellationToken ct) =>
        FromResult(await service.GetScaleAsync(ct));

    [HttpPut("scale")]
    [HasPermission(AppModules.Grading, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<GradeScaleDto>>> SaveScale([FromBody] SaveScaleRequest request, CancellationToken ct) =>
        FromResult(await service.SaveScaleAsync(request, ct));

    [HttpDelete("scale")]
    [HasPermission(AppModules.Grading, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> DeleteScale(CancellationToken ct) =>
        FromResult(await service.DeleteCampusScaleAsync(ct));

    [HttpGet("schemes")]
    [HasAnyPermission("grading:View", "results:View", "exams:View")]
    public async Task<ActionResult<ResponseDto<SchemesDto>>> Schemes([FromQuery] int? sessionId, [FromQuery] int? classId, CancellationToken ct) =>
        FromResult(await service.GetSchemesAsync(sessionId, classId, ct));

    [HttpPut("schemes")]
    [HasPermission(AppModules.Grading, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<SchemeDto>>> SaveScheme([FromBody] SaveSchemeRequest request, CancellationToken ct) =>
        FromResult(await service.SaveSchemeAsync(request, ct));

    [HttpDelete("schemes/{id:int}")]
    [HasPermission(AppModules.Grading, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> DeleteScheme(int id, CancellationToken ct) =>
        FromResult(await service.DeleteSchemeAsync(id, ct));
}
