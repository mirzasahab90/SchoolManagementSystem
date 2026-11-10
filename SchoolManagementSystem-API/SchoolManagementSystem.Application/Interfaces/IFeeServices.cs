using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.DTOs.Students;

namespace SchoolManagementSystem.Application.Interfaces;

public interface IFeeSetupService
{
    Task<Result<IReadOnlyList<FeeHeadDto>>> HeadsAsync(CancellationToken ct = default);
    Task<Result<FeeHeadDto>> CreateHeadAsync(SaveFeeHeadRequest request, CancellationToken ct = default);
    Task<Result<FeeHeadDto>> UpdateHeadAsync(int id, SaveFeeHeadRequest request, CancellationToken ct = default);
    Task<Result> DeleteHeadAsync(int id, CancellationToken ct = default);
    Task<Result<StructureGridDto>> StructureAsync(int sessionId, CancellationToken ct = default);
    Task<Result<StructureGridDto>> SaveStructureAsync(SaveStructureRequest request, CancellationToken ct = default);
    Task<Result<FeeSettingsDto>> SettingsAsync(CancellationToken ct = default);
    Task<Result<FeeSettingsDto>> SaveSettingsAsync(SaveFeeSettingsRequest request, CancellationToken ct = default);
    Task<Result> DeleteCampusSettingsAsync(CancellationToken ct = default);
}

public interface IConcessionService
{
    Task<Result<PagedResult<ConcessionDto>>> ListAsync(int sessionId, string? q, int page, int pageSize, CancellationToken ct = default);
    Task<Result<ConcessionDto>> CreateAsync(SaveConcessionRequest request, CancellationToken ct = default);
    Task<Result<ConcessionDto>> UpdateAsync(int id, SaveConcessionRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<StaffChildSuggestionDto>>> StaffChildSuggestionsAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<LineCalc>>> PreviewAsync(int studentId, DateOnly month, CancellationToken ct = default);
}

public interface IVoucherService
{
    Task<Result<GeneratePreviewDto>> PreviewAsync(GenerateRequest request, CancellationToken ct = default);
    Task<Result<GenerateResultDto>> GenerateAsync(GenerateRequest request, CancellationToken ct = default);
    Task<Result<VoucherDetailDto>> CreateSingleAsync(CreateVoucherRequest request, CancellationToken ct = default);
    Task<Result<PagedResult<VoucherDto>>> ListAsync(VoucherQuery query, CancellationToken ct = default);
    Task<Result<VoucherDetailDto>> GetAsync(int id, CancellationToken ct = default);
    Task<Result<VoucherDetailDto>> CancelAsync(int id, string? reason, CancellationToken ct = default);
    Task<Result<VoucherDetailDto>> AddAdjustmentAsync(int id, AdjustmentRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<VoucherPrintDto>>> PrintAsync(int[]? ids, DateOnly? month, int? sectionId, CancellationToken ct = default);
}

public interface IPaymentService
{
    Task<Result<IReadOnlyList<CollectLookupDto>>> LookupAsync(string? q, CancellationToken ct = default);
    Task<Result<PaymentDto>> ReceiveAsync(ReceivePaymentRequest request, CancellationToken ct = default);
    Task<Result<PaymentDto>> VoidAsync(int id, string? reason, CancellationToken ct = default);
    Task<Result<ReceiptDto>> ReceiptAsync(int id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PaymentDto>>> TodayAsync(CancellationToken ct = default);
}

public interface IFeeReportService
{
    Task<Result<DefaultersDto>> DefaultersAsync(DefaulterQuery query, CancellationToken ct = default);
    Task<Result<(byte[] Content, string FileName)>> DefaultersCsvAsync(DefaulterQuery query, CancellationToken ct = default);
    Task<Result<CollectionReportDto>> CollectionAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<Result<(byte[] Content, string FileName)>> CollectionCsvAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<Result<MonthlyReportDto>> MonthlyAsync(DateOnly month, CancellationToken ct = default);
    Task<Result<(byte[] Content, string FileName)>> MonthlyCsvAsync(DateOnly month, CancellationToken ct = default);
    Task<Result<HeadReportDto>> HeadsAsync(DateOnly month, CancellationToken ct = default);
    Task<Result<(byte[] Content, string FileName)>> HeadsCsvAsync(DateOnly month, CancellationToken ct = default);
    Task<Result<StudentFeesDto>> StudentFeesAsync(int studentId, CancellationToken ct = default);
}
