using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Fees;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>Monthly challans: preview / generate for a campus, single challans, listing and detail.</summary>
public partial class VoucherService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser, ChargeResolver charges)
    : IVoucherService
{
    public const string AlreadyExists = "Challan already exists for this month.";
    public const string NoStructure = "Fee structure not set for this class.";
    public const string LaterExists = "A later challan already exists.";
    private static readonly Error OutsideSession = Error.Validation("The month is outside the current session.");

    public async Task<Result<GeneratePreviewDto>> PreviewAsync(GenerateRequest r, CancellationToken ct = default)
    {
        var plan = await PlanAsync(r, ct);
        if (plan.Error != null) return plan.Error;
        return plan.Preview!;
    }

    public async Task<Result<GenerateResultDto>> GenerateAsync(GenerateRequest r, CancellationToken ct = default)
    {
        var plan = await PlanAsync(r, ct);
        if (plan.Error != null) return plan.Error;
        var todo = plan.Items.Where(i => i.Row.SkippedReason == null).ToList();
        if (todo.Count == 0) return new GenerateResultDto(0, plan.Preview!.Skipped);

        await using var tx = await repositories.BeginTransactionAsync(ct);
        var created = new List<(FeeVoucher Voucher, FeeVoucher? From)>();
        foreach (var item in todo)
            created.Add(await BuildAsync(item, plan.CampusId, plan.CampusCode, r.Month, r.IssueDate, plan.DueDate, plan.Settings, ct));
        await repositories.SaveChangesAsync(ct);
        foreach (var (voucher, from) in created)
            if (from != null) from.CarriedToVoucherId = voucher.Id;
        await repositories.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new GenerateResultDto(created.Count, plan.Preview!.Skipped);
    }

    public async Task<Result<VoucherDetailDto>> CreateSingleAsync(CreateVoucherRequest r, CancellationToken ct = default)
    {
        var student = await repositories.Students.GetTrackedAsync(r.StudentId, ct);
        if (student == null) return Error.NotFound("Student");
        if (student.Status != StudentStatus.Active) return Error.Validation("This student has left.");
        var plan = await PlanAsync(new GenerateRequest(r.Month, null, null, r.IssueDate, r.DueDate, true), ct, student.Id);
        if (plan.Error != null) return plan.Error;
        var item = plan.Items.FirstOrDefault();
        if (item == null) return Error.Validation("The student has no active enrollment in the current session.");
        if (item.Row.SkippedReason != null) return Error.Validation(item.Row.SkippedReason);

        await using var tx = await repositories.BeginTransactionAsync(ct);
        var (voucher, from) = await BuildAsync(item, plan.CampusId, plan.CampusCode, r.Month, r.IssueDate, plan.DueDate, plan.Settings, ct);
        await repositories.SaveChangesAsync(ct);
        if (from != null)
        {
            from.CarriedToVoucherId = voucher.Id;
            await repositories.SaveChangesAsync(ct);
        }

        await tx.CommitAsync(ct);
        return (await DetailAsync(voucher.Id, ct))!;
    }

    public async Task<Result<PagedResult<VoucherDto>>> ListAsync(VoucherQuery q, CancellationToken ct = default) =>
        await repositories.Vouchers.ListAsync(q with { Page = Math.Max(1, q.Page), PageSize = Math.Clamp(q.PageSize, 1, 200) }, ct);

    public async Task<Result<VoucherDetailDto>> GetAsync(int id, CancellationToken ct = default)
    {
        var detail = await DetailAsync(id, ct);
        return detail == null ? Error.NotFound("Challan") : detail;
    }

    public async Task<VoucherDetailDto?> DetailAsync(int id, CancellationToken ct = default)
    {
        var dto = await repositories.Vouchers.DtoAsync(id, ct);
        if (dto == null) return null;
        var v = await repositories.Vouchers.FindAsync<FeeVoucher>(id, ct);
        return new VoucherDetailDto(dto, await repositories.Vouchers.LinesAsync(id, ct), await repositories.Vouchers.PaymentsAsync(id, ct),
            v?.CarriedToVoucherId, v?.CancelReason);
    }

    // ---------------------------------------------------------------- planning

    private sealed record PlanItem(Enrollment Enrollment, IReadOnlyList<LineCalc> Lines, FeeVoucher? OpenFrom, GenerateRowDto Row);

    private sealed record Plan(
        Error? Error, int CampusId = 0, string CampusCode = "", DateOnly DueDate = default, FeeSettingsDto? Settings = null,
        List<PlanItem>? ItemList = null, GeneratePreviewDto? Preview = null)
    {
        public List<PlanItem> Items => ItemList ?? [];
    }

    private async Task<Plan> PlanAsync(GenerateRequest r, CancellationToken ct, int? onlyStudentId = null)
    {
        if (currentUser.ActiveCampusId is not int campusId) return new(Error.Validation("Select a campus."));
        var month = new DateOnly(r.Month.Year, r.Month.Month, 1);
        if (r.Month != month) return new(Error.Validation("Month must be the first day of the month."));
        var session = await repositories.Sessions.GetEffectiveCurrentAsync(campusId, ct);
        if (session == null || month < new DateOnly(session.StartDate.Year, session.StartDate.Month, 1) || month > session.EndDate)
            return new(OutsideSession);

        var settings = await charges.SettingsAsync(campusId, ct);
        var due = r.DueDate ?? new DateOnly(month.Year, month.Month, Math.Min(settings.DueDay, DateTime.DaysInMonth(month.Year, month.Month)));
        if (due < r.IssueDate) return new(Error.Validation("The due date cannot be before the issue date."));
        var campus = await repositories.Campuses.FindAsync<Domain.Entities.Org.Campus>(campusId, ct);

        var roster = await repositories.Vouchers.RosterAsync(campusId, session.Id, r.ClassId, r.SectionId, ct);
        if (onlyStudentId is int sid) roster = roster.Where(e => e.StudentId == sid).ToList();
        var ids = roster.Select(e => e.StudentId).ToList();
        var existing = await repositories.Vouchers.StudentsWithVoucherAsync(ids, month, ct);
        var later = await repositories.Vouchers.StudentsWithLaterAsync(ids, month, ct);
        var open = await repositories.Vouchers.OpenBeforeAsync(ids, month, ct);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var items = new List<PlanItem>();
        foreach (var e in roster)
        {
            string? skip = null;
            IReadOnlyList<LineCalc> lines = [];
            if (existing.Contains(e.StudentId)) skip = AlreadyExists;
            else if (later.Contains(e.StudentId)) skip = LaterExists;
            else if (!await charges.HasStructureAsync(campusId, session.Id, e.Section.ClassId, ct)) skip = NoStructure;
            else lines = await charges.LinesAsync(e.StudentId, campusId, e.Student.AdmissionDate, session.Id, e.Section.ClassId, month, session, ct);

            var from = skip == null ? open.GetValueOrDefault(e.StudentId) : null;
            var arrears = from == null ? 0 : VoucherLedger.ArrearsOf(from, today);
            items.Add(new PlanItem(e, lines, from, new GenerateRowDto(
                e.StudentId, e.Student.FirstName + " " + e.Student.LastName, e.Student.AdmissionNo, e.Section.Class.Name, e.Section.Name,
                lines, arrears, lines.Sum(l => l.Net) + arrears, skip)));
        }

        var rows = items.Select(i => i.Row).ToList();
        var toCreate = rows.Count(x => x.SkippedReason == null);
        var preview = new GeneratePreviewDto(month, due, toCreate, rows.Count - toCreate, rows.Where(x => x.SkippedReason == null).Sum(x => x.Total), rows);
        return new(null, campusId, campus?.Code ?? "C", due, settings, items, preview);
    }

    private async Task<(FeeVoucher Voucher, FeeVoucher? From)> BuildAsync(
        PlanItem item, int campusId, string campusCode, DateOnly month, DateOnly issue, DateOnly due, FeeSettingsDto? settings, CancellationToken ct)
    {
        var no = await repositories.Sequences.NextAsync(campusId, "fee-voucher", ct);
        var v = new FeeVoucher
        {
            CampusId = campusId, VoucherNo = $"{campusCode}-F-{no:000000}", StudentId = item.Enrollment.StudentId,
            EnrollmentId = item.Enrollment.Id, AcademicSessionId = item.Enrollment.AcademicSessionId, Month = new DateOnly(month.Year, month.Month, 1),
            IssueDate = issue, DueDate = due, LateFineAmount = settings?.LateFine ?? 0,
        };
        foreach (var l in item.Lines)
            v.Lines.Add(new FeeVoucherLine
            {
                Kind = VoucherLineKind.Head, FeeHeadId = l.FeeHeadId, Description = l.Description, Amount = l.Amount,
                Concession = l.Concession, ConcessionNote = l.ConcessionNote, Net = l.Net,
            });
        if (item.OpenFrom != null) VoucherLedger.CarryForward(item.OpenFrom, v, DateOnly.FromDateTime(DateTime.Today));
        VoucherLedger.Recompute(v);
        repositories.Vouchers.Add(v);
        return (v, item.OpenFrom);
    }
}
