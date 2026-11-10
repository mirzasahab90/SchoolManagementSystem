using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>Cancel, adjustment and print data.</summary>
public partial class VoucherService
{
    public static readonly Error Closed = Error.Validation("This challan is closed.");
    private static readonly Error NotLatest = Error.Validation("Only the latest challan of a student can be cancelled.");
    private static readonly Error HasPayments = Error.Validation("This challan has payments. Void them first.");
    private static readonly Error BelowPaid = Error.Validation("Adjustment would take the total below the amount already paid.");

    public async Task<Result<VoucherDetailDto>> CancelAsync(int id, string? reason, CancellationToken ct = default)
    {
        var text = reason?.Trim() ?? "";
        if (text.Length is 0 or > 200) return Error.Validation("Give a reason (200 characters at most).");
        var v = await repositories.Vouchers.GetTrackedAsync(id, ct);
        if (v == null) return Error.NotFound("Challan");
        if (v.Status == VoucherStatus.Cancelled) return Closed;
        if (v.Status == VoucherStatus.CarriedForward || await repositories.Vouchers.HasLaterAsync(v.StudentId, v.Month, ct)) return NotLatest;
        if (v.Payments.Any(p => !p.IsVoid)) return HasPayments;

        foreach (var from in await repositories.Vouchers.CarriedIntoTrackedAsync(v.Id, ct))
        {
            // A fine that went into the cancelled arrears is owed again by the reopened challan.
            if (!from.Lines.Any(l => l.Kind == VoucherLineKind.LateFine)) from.LateFineApplied = false;
            VoucherLedger.Reopen(from);
        }

        v.Status = VoucherStatus.Cancelled;
        v.CancelReason = text;
        await repositories.SaveChangesAsync(ct);
        return (await DetailAsync(id, ct))!;
    }

    public async Task<Result<VoucherDetailDto>> AddAdjustmentAsync(int id, AdjustmentRequest r, CancellationToken ct = default)
    {
        var text = r.Reason?.Trim() ?? "";
        if (text.Length is 0 or > 120) return Error.Validation("Give a reason (120 characters at most).");
        if (r.Amount == 0 || Math.Abs(r.Amount) > 10_000_000) return Error.Validation("Enter an amount other than 0.");
        var v = await repositories.Vouchers.GetTrackedAsync(id, ct);
        if (v == null) return Error.NotFound("Challan");
        if (!v.IsOpen) return Closed;
        var amount = decimal.Round(r.Amount, 2);
        if (v.Total + amount < v.Paid || v.Total + amount < 0) return BelowPaid;
        v.Lines.Add(new FeeVoucherLine { Kind = VoucherLineKind.Adjustment, Description = text, Amount = amount, Net = amount });
        VoucherLedger.Recompute(v);
        await repositories.SaveChangesAsync(ct);
        return (await DetailAsync(id, ct))!;
    }

    public async Task<Result<IReadOnlyList<VoucherPrintDto>>> PrintAsync(int[]? ids, DateOnly? month, int? sectionId, CancellationToken ct = default)
    {
        List<VoucherDto> list;
        if (ids is { Length: > 0 })
        {
            if (ids.Length > 500) return Error.Validation("Print at most 500 challans at a time.");
            list = await repositories.Vouchers.DtosAsync(ids, ct);
        }
        else if (month != null)
        {
            list = (await repositories.Vouchers.ListAsync(new VoucherQuery(month, null, sectionId, null, null, 1, 1000), ct)).Items.ToList();
        }
        else return Error.Validation("Pick challans or a month.");

        list = list.Where(v => v.Status != VoucherStatus.Cancelled).ToList();
        var school = await repositories.Schools.GetTrackedAsync(ct);
        var meta = await repositories.Vouchers.PrintMetaAsync(list.Select(v => v.Id).ToList(), ct);
        var outList = new List<VoucherPrintDto>();
        foreach (var v in list)
        {
            var m = meta[v.Id];
            var settings = await charges.SettingsAsync(m.CampusId, ct);
            outList.Add(new VoucherPrintDto(school?.Name ?? "", m.CampusName, m.CampusAddress, m.CampusPhone, settings.BankName,
                settings.AccountTitle, settings.AccountNo, settings.ChallanNote, v, m.GuardianName, await repositories.Vouchers.LinesAsync(v.Id, ct)));
        }

        return outList;
    }
}
