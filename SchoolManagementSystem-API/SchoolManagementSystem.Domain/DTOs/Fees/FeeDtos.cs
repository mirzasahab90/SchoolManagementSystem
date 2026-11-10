using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Domain.DTOs.Fees;

// ---------------------------------------------------------------- setup

public record FeeHeadDto(int Id, int? CampusId, string Name, FeeFrequency Frequency, int? OneTimeMonth, int SortOrder, bool IsActive);

public record SaveFeeHeadRequest(string Name, FeeFrequency Frequency, int? OneTimeMonth, int SortOrder, bool IsActive, bool MainCampus);

public record StructureCellDto(int FeeHeadId, decimal? MainAmount, decimal? CampusAmount, decimal? Effective);

public record StructureRowDto(int ClassId, string ClassName, IReadOnlyList<StructureCellDto> Cells);

public record StructureGridDto(int SessionId, IReadOnlyList<FeeHeadDto> Heads, IReadOnlyList<StructureRowDto> Rows);

public record StructureCellSave(int ClassId, int FeeHeadId, decimal? Amount);

public record SaveStructureRequest(int SessionId, bool MainCampus, IReadOnlyList<StructureCellSave> Rows);

public record FeeSettingsDto(
    int DueDay, decimal LateFine, decimal SiblingSecondPercent, decimal SiblingThirdPlusPercent, decimal StaffChildPercent,
    IReadOnlyList<int> ConcessionHeadIds, string? BankName, string? AccountTitle, string? AccountNo, string? ChallanNote,
    bool HasCampusOverride, IReadOnlyList<string> OverriddenFields);

public record SaveFeeSettingsRequest(
    int? DueDay, decimal? LateFine, decimal? SiblingSecondPercent, decimal? SiblingThirdPlusPercent, decimal? StaffChildPercent,
    IReadOnlyList<int>? ConcessionHeadIds, string? BankName, string? AccountTitle, string? AccountNo, string? ChallanNote,
    bool MainCampus);

// ---------------------------------------------------------------- concessions and calculation

public record HeadCharge(int FeeHeadId, string Name, FeeFrequency Frequency, decimal Amount);

public record ConcessionCandidate(ConcessionKind Kind, int? FeeHeadId, ConcessionValueType ValueType, decimal Value, string Reason);

public record LineCalc(int FeeHeadId, string Description, decimal Amount, decimal Concession, string? ConcessionNote, decimal Net);

public record ConcessionDto(
    int Id, int StudentId, string StudentName, string AdmissionNo, string? ClassName, string? SectionName, int SessionId,
    int? FeeHeadId, string HeadName, ConcessionKind Kind, int? StaffId, string? StaffName, ConcessionValueType ValueType,
    decimal Value, string Reason, bool IsActive);

public record SaveConcessionRequest(
    int StudentId, int SessionId, int? FeeHeadId, ConcessionKind Kind, int? StaffId, ConcessionValueType ValueType, decimal? Value,
    string Reason, bool IsActive = true);

public record StaffChildSuggestionDto(int StudentId, string StudentName, string ClassName, int StaffId, string StaffName, string MaskedCnic);

// ---------------------------------------------------------------- vouchers

public record GenerateRequest(DateOnly Month, int? ClassId, int? SectionId, DateOnly IssueDate, DateOnly? DueDate, bool Preview);

public record GenerateRowDto(
    int StudentId, string Name, string AdmissionNo, string ClassName, string SectionName, IReadOnlyList<LineCalc> Lines, decimal Arrears,
    decimal Total, string? SkippedReason);

public record GeneratePreviewDto(DateOnly Month, DateOnly DueDate, int ToCreate, int Skipped, decimal Total, IReadOnlyList<GenerateRowDto> Rows);

public record GenerateResultDto(int Created, int Skipped);

public record CreateVoucherRequest(int StudentId, DateOnly Month, DateOnly IssueDate, DateOnly? DueDate);

public record VoucherQuery(DateOnly? Month, int? ClassId, int? SectionId, VoucherStatus? Status, string? Q, int Page, int PageSize);

public record VoucherDto(
    int Id, string VoucherNo, int StudentId, string StudentName, string AdmissionNo, string ClassName, string SectionName, int RollNo,
    DateOnly Month, DateOnly IssueDate, DateOnly DueDate, decimal Total, decimal LateFineAmount, bool LateFineApplied, decimal Paid,
    decimal Balance, decimal PayableAfterDue, VoucherStatus Status);

public record VoucherLineDto(int Id, VoucherLineKind Kind, string Description, decimal Amount, decimal Concession, string? ConcessionNote, decimal Net);

public record PaymentDto(
    int Id, string ReceiptNo, int VoucherId, string VoucherNo, decimal Amount, DateOnly PaidOn, PaymentMethod Method, string? Reference,
    string ReceivedBy, bool IsVoid, string? VoidReason);

public record VoucherDetailDto(
    VoucherDto Voucher, IReadOnlyList<VoucherLineDto> Lines, IReadOnlyList<PaymentDto> Payments, int? CarriedToVoucherId, string? CancelReason);

public record ReasonRequest(string Reason);

public record AdjustmentRequest(decimal Amount, string Reason);

public record VoucherPrintDto(
    string SchoolName, string CampusName, string? CampusAddress, string? CampusPhone, string? BankName, string? AccountTitle,
    string? AccountNo, string? ChallanNote, VoucherDto Voucher, string? GuardianName, IReadOnlyList<VoucherLineDto> Lines);

// ---------------------------------------------------------------- payments

public record ReceivePaymentRequest(int VoucherId, decimal Amount, DateOnly PaidOn, PaymentMethod Method, string? Reference);

public record CollectLookupDto(
    int StudentId, string Name, string AdmissionNo, string ClassName, string SectionName, string? GuardianName, string? GuardianPhone,
    bool StudentLeft, VoucherDetailDto? OpenVoucher, decimal FineIfPaidToday);

public record ReceiptDto(string SchoolName, string CampusName, PaymentDto Payment, VoucherDto Voucher, string StudentName, string ClassName, decimal BalanceAfter);

// ---------------------------------------------------------------- reports

public record DefaulterQuery(int? ClassId, int? SectionId, int? MinMonths, decimal? MinAmount, int Page, int PageSize);

public record DefaulterDto(
    int StudentId, string Name, string AdmissionNo, string ClassName, string SectionName, string? GuardianPhone, int Months, decimal Balance,
    DateOnly OldestMonth, bool StudentLeft, int VoucherId);

public record DefaultersDto(IReadOnlyList<DefaulterDto> Items, int Total, decimal TotalBalance);

public record MethodTotal(PaymentMethod Method, int Count, decimal Total);

public record UserTotal(string User, int Count, decimal Total);

public record CampusTotalDto(string CampusCode, string CampusName, decimal Billed, decimal Collected, decimal Outstanding);

public record CollectionReportDto(
    DateOnly From, DateOnly To, IReadOnlyList<PaymentDto> Payments, IReadOnlyList<MethodTotal> ByMethod, IReadOnlyList<UserTotal> ByUser,
    decimal Total, decimal Voided, IReadOnlyList<CampusTotalDto>? Campuses);

public record MonthlyClassRow(string ClassName, int Vouchers, decimal Billed, decimal Concession, decimal Collected, decimal Outstanding, decimal CollectionPercent);

public record MonthlyReportDto(DateOnly Month, IReadOnlyList<MonthlyClassRow> Rows, MonthlyClassRow Totals, IReadOnlyList<CampusTotalDto>? Campuses);

public record HeadRow(string Head, decimal Amount, decimal Concession, decimal Net);

public record HeadReportDto(DateOnly Month, IReadOnlyList<HeadRow> Rows);

public record StudentFeesDto(decimal Balance, IReadOnlyList<VoucherDto> Vouchers, IReadOnlyList<PaymentDto> Payments);
