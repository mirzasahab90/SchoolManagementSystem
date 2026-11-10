using Microsoft.AspNetCore.Mvc;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Hiring;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Hiring;

namespace SchoolManagementSystem.Api.Controllers;

public class VacanciesController(IVacancyService service) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Vacancies, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<VacancyDto>>>> List([FromQuery] VacancyStatus? status, CancellationToken ct) =>
        FromResult(await service.ListAsync(status, ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Vacancies, AppActions.View)]
    public async Task<ActionResult<ResponseDto<VacancyDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Vacancies, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<VacancyDto>>> Create([FromBody] SaveVacancyRequest request, CancellationToken ct) =>
        FromResult(await service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Vacancies, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<VacancyDto>>> Update(int id, [FromBody] SaveVacancyRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, request, ct));

    [HttpPost("{id:int}/status")]
    [HasPermission(AppModules.Vacancies, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<VacancyDto>>> SetStatus(int id, [FromBody] VacancyStatusRequest request, CancellationToken ct) =>
        FromResult(await service.SetStatusAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Vacancies, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));
}

public class CandidatesController(ICandidateService service) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Candidates, AppActions.View)]
    public async Task<ActionResult<ResponseDto<PagedResult<CandidateListItemDto>>>> List(
        [FromQuery] string? q, [FromQuery] int? vacancyId, [FromQuery] string? stage, [FromQuery] CandidateSource? source,
        [FromQuery] DateOnly? appliedFrom, [FromQuery] DateOnly? appliedTo,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        FromResult(await service.SearchAsync(vacancyId, stage, q, source, appliedFrom, appliedTo, page, Math.Clamp(pageSize, 1, 100), ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Candidates, AppActions.View)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await service.GetProfileAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Candidates, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Create([FromBody] SaveCandidateRequest request, CancellationToken ct) =>
        FromResult(await service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Update(int id, [FromBody] SaveCandidateRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Candidates, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPost("{id:int}/move")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Move(int id, [FromBody] MoveCandidateRequest request, CancellationToken ct) =>
        FromResult(await service.MoveAsync(id, request, ct));

    [HttpPost("{id:int}/cv")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    [RequestSizeLimit(11_000_000)]
    public async Task<ActionResult<ResponseDto<object>>> SetCv(int id, IFormFile file, CancellationToken ct) =>
        FromResult(await service.SetCvAsync(id, file, ct));

    [HttpGet("{id:int}/cv")]
    [HasPermission(AppModules.Candidates, AppActions.View)]
    public async Task<IActionResult> GetCv(int id, CancellationToken ct)
    {
        var result = await service.GetCvAsync(id, ct);
        return result.IsSuccess
            ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName)
            : Failure<object>(result.Error!);
    }

    [HttpPost("{id:int}/interviews")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Schedule(int id, [FromBody] ScheduleInterviewRequest request, CancellationToken ct) =>
        FromResult(await service.ScheduleInterviewAsync(id, request, ct));

    [HttpPut("{id:int}/interviews/{roundId:int}")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> UpdateRound(int id, int roundId, [FromBody] UpdateInterviewRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateInterviewAsync(id, roundId, request, ct));

    [HttpDelete("{id:int}/interviews/{roundId:int}")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> DeleteRound(int id, int roundId, CancellationToken ct) =>
        FromResult(await service.DeleteInterviewAsync(id, roundId, ct));

    [HttpPost("{id:int}/offer")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Offer(int id, [FromBody] MakeOfferRequest request, CancellationToken ct) =>
        FromResult(await service.MakeOfferAsync(id, request, ct));

    [HttpPost("{id:int}/offer/accept")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Accept(int id, CancellationToken ct) =>
        FromResult(await service.RespondOfferAsync(id, OfferStatus.Accepted, ct));

    [HttpPost("{id:int}/offer/decline")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Decline(int id, CancellationToken ct) =>
        FromResult(await service.RespondOfferAsync(id, OfferStatus.Declined, ct));

    [HttpPost("{id:int}/offer/withdraw")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<CandidateProfileDto>>> Withdraw(int id, CancellationToken ct) =>
        FromResult(await service.RespondOfferAsync(id, OfferStatus.Withdrawn, ct));

    [HttpPost("{id:int}/hire")]
    [HasPermission(AppModules.Candidates, AppActions.Edit)]
    [HasPermission(AppModules.Staff, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<StaffDetailDto>>> Hire(int id, [FromBody] HireRequest request, CancellationToken ct) =>
        FromResult(await service.HireAsync(id, request, ct));
}

public class StaffController(IStaffService service) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.Staff, AppActions.View)]
    public async Task<ActionResult<ResponseDto<PagedResult<StaffListItemDto>>>> List(
        [FromQuery] string? q, [FromQuery] StaffStatus? status, [FromQuery] StaffCategory? category,
        [FromQuery] StaffDesignation? designation, [FromQuery] string? sort, [FromQuery] string? dir,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        FromResult(await service.SearchAsync(q, status, category, designation, sort, dir, page, Math.Clamp(pageSize, 1, 100), ct));

    /// <summary>Distinct designations of the staff the caller can see (for the list filter).</summary>
    [HttpGet("designations")]
    [HasPermission(AppModules.Staff, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<StaffDesignation>>>> Designations(CancellationToken ct) =>
        FromResult(await service.DesignationsAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(AppModules.Staff, AppActions.View)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await service.GetProfileAsync(id, ct));

    [HttpGet("{id:int}/bank")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StaffBankDto>>> GetBank(int id, CancellationToken ct) =>
        FromResult(await service.GetBankAsync(id, ct));

    [HttpPost]
    [HasPermission(AppModules.Staff, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> Create([FromBody] SaveStaffRequest request, CancellationToken ct) =>
        FromResult(await service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> Update(int id, [FromBody] SaveStaffRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.Staff, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpPost("{id:int}/photo")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    [RequestSizeLimit(11_000_000)]
    public async Task<ActionResult<ResponseDto<object>>> SetPhoto(int id, IFormFile file, CancellationToken ct) =>
        FromResult(await service.SetPhotoAsync(id, file, ct));

    [HttpGet("{id:int}/photo")]
    [HasPermission(AppModules.Staff, AppActions.View)]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken ct)
    {
        var result = await service.GetPhotoAsync(id, ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType) : Failure<object>(result.Error!);
    }

    [HttpPost("{id:int}/documents")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    [RequestSizeLimit(11_000_000)]
    public async Task<ActionResult<ResponseDto<StaffDocumentDto>>> UploadDocument(
        int id, [FromForm] StaffDocumentType type, IFormFile file, CancellationToken ct) =>
        FromResult(await service.UploadDocumentAsync(id, type, file, ct));

    [HttpGet("{id:int}/documents/{docId:int}")]
    [HasPermission(AppModules.Staff, AppActions.View)]
    public async Task<IActionResult> GetDocument(int id, int docId, CancellationToken ct)
    {
        var result = await service.GetDocumentAsync(id, docId, ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : Failure<object>(result.Error!);
    }

    [HttpPost("{id:int}/checklist/{item}")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> Checklist(int id, string item, [FromBody] ChecklistRequest request, CancellationToken ct) =>
        FromResult(await service.SetChecklistAsync(id, item, request.Done, ct));

    [HttpPost("{id:int}/account")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    [HasPermission(AppModules.Users, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> CreateLogin(int id, [FromBody] CreateStaffLoginRequest request, CancellationToken ct) =>
        FromResult(await service.CreateLoginAsync(id, request, ct));

    [HttpPost("{id:int}/account/link")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    [HasPermission(AppModules.Users, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> LinkLogin(int id, [FromBody] LinkStaffLoginRequest request, CancellationToken ct) =>
        FromResult(await service.LinkLoginAsync(id, request, ct));

    [HttpGet("{id:int}/assignments/options")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<AssignmentOptionsDto>>> AssignmentOptions(int id, CancellationToken ct) =>
        FromResult(await service.GetAssignmentOptionsAsync(id, ct));

    [HttpPost("{id:int}/assignments")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> Assign(int id, [FromBody] AssignRequest request, CancellationToken ct) =>
        FromResult(await service.AssignAsync(id, request, ct));

    [HttpDelete("{id:int}/assignments/{assignmentId:int}")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> EndAssignment(int id, int assignmentId, CancellationToken ct) =>
        FromResult(await service.EndAssignmentAsync(id, assignmentId, ct));

    [HttpPost("{id:int}/status")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StaffProfileDto>>> SetStatus(int id, [FromBody] StaffStatusRequest request, CancellationToken ct) =>
        FromResult(await service.SetStatusAsync(id, request, ct));

    [HttpDelete("{id:int}/documents/{docId:int}")]
    [HasPermission(AppModules.Staff, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<object>>> DeleteDocument(int id, int docId, CancellationToken ct) =>
        FromResult(await service.DeleteDocumentAsync(id, docId, ct));
}
