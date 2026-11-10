using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SchoolManagementSystem.Api.Infrastructure;
using SchoolManagementSystem.Api.Security;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Domain.Constants;
using SchoolManagementSystem.Domain.DTOs;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Api.Controllers;

[Route("api/fees")]
public class FeeSetupController(IFeeSetupService service) : ApiControllerBase
{
    private const string AnyFeeView = "fee-setup:View";

    [HttpGet("heads")]
    [HasAnyPermission(AnyFeeView, "fee-vouchers:View", "fee-collection:View", "fee-reports:View")]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<FeeHeadDto>>>> Heads(CancellationToken ct) =>
        FromResult(await service.HeadsAsync(ct));

    [HttpPost("heads")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<FeeHeadDto>>> CreateHead([FromBody] SaveFeeHeadRequest request, CancellationToken ct) =>
        FromResult(await service.CreateHeadAsync(request, ct));

    [HttpPut("heads/{id:int}")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<FeeHeadDto>>> UpdateHead(int id, [FromBody] SaveFeeHeadRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateHeadAsync(id, request, ct));

    [HttpDelete("heads/{id:int}")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<object>>> DeleteHead(int id, CancellationToken ct) =>
        FromResult(await service.DeleteHeadAsync(id, ct));

    [HttpGet("structure")]
    [HasPermission(AppModules.FeeSetup, AppActions.View)]
    public async Task<ActionResult<ResponseDto<StructureGridDto>>> Structure([FromQuery] int sessionId, CancellationToken ct) =>
        FromResult(await service.StructureAsync(sessionId, ct));

    [HttpPut("structure")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<StructureGridDto>>> SaveStructure([FromBody] SaveStructureRequest request, CancellationToken ct) =>
        FromResult(await service.SaveStructureAsync(request, ct));

    [HttpGet("settings")]
    [HasAnyPermission(AnyFeeView, "fee-vouchers:View", "fee-collection:View", "fee-reports:View")]
    public async Task<ActionResult<ResponseDto<FeeSettingsDto>>> Settings(CancellationToken ct) =>
        FromResult(await service.SettingsAsync(ct));

    [HttpPut("settings")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<FeeSettingsDto>>> SaveSettings([FromBody] SaveFeeSettingsRequest request, CancellationToken ct) =>
        FromResult(await service.SaveSettingsAsync(request, ct));

    [HttpDelete("settings")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<object>>> DeleteSettings(CancellationToken ct) =>
        FromResult(await service.DeleteCampusSettingsAsync(ct));
}

[Route("api/fees/concessions")]
public class ConcessionsController(IConcessionService service) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.FeeSetup, AppActions.View)]
    public async Task<ActionResult<ResponseDto<PagedResult<ConcessionDto>>>> List(
        [FromQuery] int sessionId, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        FromResult(await service.ListAsync(sessionId, q, page, pageSize, ct));

    [HttpPost]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<ConcessionDto>>> Create([FromBody] SaveConcessionRequest request, CancellationToken ct) =>
        FromResult(await service.CreateAsync(request, ct));

    [HttpPut("{id:int}")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<ConcessionDto>>> Update(int id, [FromBody] SaveConcessionRequest request, CancellationToken ct) =>
        FromResult(await service.UpdateAsync(id, request, ct));

    [HttpDelete("{id:int}")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<object>>> Delete(int id, CancellationToken ct) =>
        FromResult(await service.DeleteAsync(id, ct));

    [HttpGet("staff-child-suggestions")]
    [HasPermission(AppModules.FeeSetup, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<StaffChildSuggestionDto>>>> Suggestions(CancellationToken ct) =>
        FromResult(await service.StaffChildSuggestionsAsync(ct));

    [HttpGet("preview")]
    [HasPermission(AppModules.FeeSetup, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<LineCalc>>>> Preview([FromQuery] int studentId, [FromQuery] DateOnly month, CancellationToken ct) =>
        FromResult(await service.PreviewAsync(studentId, month, ct));
}

[Route("api/fees/vouchers")]
public class VouchersController(IVoucherService service) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(AppModules.FeeVouchers, AppActions.View)]
    public async Task<ActionResult<ResponseDto<PagedResult<VoucherDto>>>> List(
        [FromQuery] DateOnly? month, [FromQuery] int? classId, [FromQuery] int? sectionId, [FromQuery] VoucherStatus? status,
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        FromResult(await service.ListAsync(new VoucherQuery(month, classId, sectionId, status, q, page, pageSize), ct));

    [HttpGet("{id:int}")]
    [HasAnyPermission("fee-vouchers:View", "fee-collection:View")]
    public async Task<ActionResult<ResponseDto<VoucherDetailDto>>> Get(int id, CancellationToken ct) =>
        FromResult(await service.GetAsync(id, ct));

    /// <summary>Preview needs View; writing challans needs Add.</summary>
    [HttpPost("generate")]
    [HasPermission(AppModules.FeeVouchers, AppActions.View)]
    public async Task<IActionResult> Generate([FromBody] GenerateRequest request, [FromServices] IPermissionChecker permissions, CancellationToken ct)
    {
        if (request.Preview) return ((IConvertToActionResult)FromResult(await service.PreviewAsync(request, ct))).Convert();
        if (!await permissions.HasPermissionAsync(CurrentUserId, AppModules.FeeVouchers, AppActions.Add, ct))
            return Failure<object>(Domain.Common.Error.Forbidden());
        return ((IConvertToActionResult)FromResult(await service.GenerateAsync(request, ct))).Convert();
    }

    [HttpPost]
    [HasPermission(AppModules.FeeVouchers, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<VoucherDetailDto>>> Create([FromBody] CreateVoucherRequest request, CancellationToken ct) =>
        FromResult(await service.CreateSingleAsync(request, ct));

    [HttpPost("{id:int}/cancel")]
    [HasPermission(AppModules.FeeVouchers, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<VoucherDetailDto>>> Cancel(int id, [FromBody] ReasonRequest request, CancellationToken ct) =>
        FromResult(await service.CancelAsync(id, request.Reason, ct));

    [HttpPost("{id:int}/adjustments")]
    [HasPermission(AppModules.FeeVouchers, AppActions.Edit)]
    public async Task<ActionResult<ResponseDto<VoucherDetailDto>>> Adjust(int id, [FromBody] AdjustmentRequest request, CancellationToken ct) =>
        FromResult(await service.AddAdjustmentAsync(id, request, ct));

    [HttpGet("print")]
    [HasAnyPermission("fee-vouchers:View", "fee-collection:View")]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<VoucherPrintDto>>>> Print(
        [FromQuery] string? ids, [FromQuery] DateOnly? month, [FromQuery] int? sectionId, CancellationToken ct)
    {
        int[]? list = null;
        if (!string.IsNullOrWhiteSpace(ids))
        {
            var parts = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Any(x => !int.TryParse(x, out _))) return Failure<IReadOnlyList<VoucherPrintDto>>(Domain.Common.Error.Validation("Bad challan ids."));
            list = parts.Select(int.Parse).ToArray();
        }

        return FromResult(await service.PrintAsync(list, month, sectionId, ct));
    }
}

[Route("api/fees")]
public class PaymentsController(IPaymentService service) : ApiControllerBase
{
    [HttpGet("collect/lookup")]
    [HasPermission(AppModules.FeeCollection, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<CollectLookupDto>>>> Lookup([FromQuery] string? q, CancellationToken ct) =>
        FromResult(await service.LookupAsync(q, ct));

    [HttpPost("payments")]
    [HasPermission(AppModules.FeeCollection, AppActions.Add)]
    public async Task<ActionResult<ResponseDto<PaymentDto>>> Receive([FromBody] ReceivePaymentRequest request, CancellationToken ct) =>
        FromResult(await service.ReceiveAsync(request, ct));

    [HttpPost("payments/{id:int}/void")]
    [HasPermission(AppModules.FeeCollection, AppActions.Delete)]
    public async Task<ActionResult<ResponseDto<PaymentDto>>> Void(int id, [FromBody] ReasonRequest request, CancellationToken ct) =>
        FromResult(await service.VoidAsync(id, request.Reason, ct));

    [HttpGet("payments/{id:int}/receipt")]
    [HasAnyPermission("fee-collection:View", "fee-vouchers:View")]
    public async Task<ActionResult<ResponseDto<ReceiptDto>>> Receipt(int id, CancellationToken ct) =>
        FromResult(await service.ReceiptAsync(id, ct));

    [HttpGet("payments/today")]
    [HasPermission(AppModules.FeeCollection, AppActions.View)]
    public async Task<ActionResult<ResponseDto<IReadOnlyList<PaymentDto>>>> Today(CancellationToken ct) =>
        FromResult(await service.TodayAsync(ct));
}

[Route("api/fees")]
public class FeeReportsController(IFeeReportService service) : ApiControllerBase
{
    private const string View = "fee-reports";

    [HttpGet("defaulters")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<ActionResult<ResponseDto<DefaultersDto>>> Defaulters(
        [FromQuery] int? classId, [FromQuery] int? sectionId, [FromQuery] int? minMonths, [FromQuery] decimal? minAmount,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) =>
        FromResult(await service.DefaultersAsync(new DefaulterQuery(classId, sectionId, minMonths, minAmount, page, pageSize), ct));

    [HttpGet("defaulters/csv")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<IActionResult> DefaultersCsv(
        [FromQuery] int? classId, [FromQuery] int? sectionId, [FromQuery] int? minMonths, [FromQuery] decimal? minAmount, CancellationToken ct) =>
        Csv(await service.DefaultersCsvAsync(new DefaulterQuery(classId, sectionId, minMonths, minAmount, 1, 100_000), ct));

    [HttpGet("reports/collection")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<ActionResult<ResponseDto<CollectionReportDto>>> Collection([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        FromResult(await service.CollectionAsync(from, to, ct));

    [HttpGet("reports/collection/csv")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<IActionResult> CollectionCsv([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Csv(await service.CollectionCsvAsync(from, to, ct));

    [HttpGet("reports/monthly")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<ActionResult<ResponseDto<MonthlyReportDto>>> Monthly([FromQuery] DateOnly month, CancellationToken ct) =>
        FromResult(await service.MonthlyAsync(month, ct));

    [HttpGet("reports/monthly/csv")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<IActionResult> MonthlyCsv([FromQuery] DateOnly month, CancellationToken ct) =>
        Csv(await service.MonthlyCsvAsync(month, ct));

    [HttpGet("reports/heads")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<ActionResult<ResponseDto<HeadReportDto>>> Heads([FromQuery] DateOnly month, CancellationToken ct) =>
        FromResult(await service.HeadsAsync(month, ct));

    [HttpGet("reports/heads/csv")]
    [HasPermission(AppModules.FeeReports, AppActions.View)]
    public async Task<IActionResult> HeadsCsv([FromQuery] DateOnly month, CancellationToken ct) =>
        Csv(await service.HeadsCsvAsync(month, ct));

    private IActionResult Csv(Domain.Common.Result<(byte[] Content, string FileName)> result) =>
        result.IsSuccess ? File(result.Value.Content, "text/csv; charset=utf-8", result.Value.FileName) : Failure<object>(result.Error!);
}
