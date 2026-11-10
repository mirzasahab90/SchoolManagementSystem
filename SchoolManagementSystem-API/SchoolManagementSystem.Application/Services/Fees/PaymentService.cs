using System.Globalization;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>Counter and bank payments against a challan, receipts and voids.</summary>
public class PaymentService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser, VoucherService vouchers) : IPaymentService
{
    public async Task<Result<IReadOnlyList<CollectLookupDto>>> LookupAsync(string? q, CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId == null) return Error.Validation("Select a campus.");
        var list = new List<CollectLookupDto>();
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return list;
        var ids = await repositories.Vouchers.LookupStudentIdsAsync(q, ct);
        var today = Today;
        foreach (var s in await repositories.Vouchers.LookupStudentsAsync(ids, ct))
        {
            VoucherDetailDto? open = null;
            decimal fine = 0;
            if (s.OpenVoucherId is int vid)
            {
                open = await vouchers.DetailAsync(vid, ct);
                var v = await repositories.Vouchers.FindAsync<FeeVoucher>(vid, ct);
                if (v != null) fine = VoucherLedger.FineOn(v, today);
            }

            list.Add(new CollectLookupDto(s.Id, s.Name, s.AdmissionNo, s.ClassName ?? "-", s.SectionName ?? "-", s.GuardianName, s.GuardianPhone,
                s.Left, open, fine));
        }

        return list;
    }

    public async Task<Result<PaymentDto>> ReceiveAsync(ReceivePaymentRequest r, CancellationToken ct = default)
    {
        if (r.Amount <= 0) return Error.Validation("Enter an amount above 0.");
        if (r.PaidOn > Today) return Error.Validation("The payment date cannot be in the future.");
        if (r.Reference?.Trim().Length > 60) return Error.Validation("Reference is 60 characters at most.");
        var v = await repositories.Vouchers.GetTrackedAsync(r.VoucherId, ct);
        if (v == null) return Error.NotFound("Challan");
        if (!v.IsOpen) return VoucherService.Closed;

        var addedFine = VoucherLedger.ApplyLateFine(v, r.PaidOn);
        var amount = decimal.Round(r.Amount, 2);
        if (amount > v.Balance)
            return Error.Validation($"Amount is more than the balance of Rs {v.Balance.ToString("N0", CultureInfo.InvariantCulture)}.");

        var no = await repositories.Sequences.NextAsync(v.CampusId, "fee-receipt", ct);
        var campus = await repositories.Campuses.FindAsync<Domain.Entities.Org.Campus>(v.CampusId, ct);
        var payment = new FeePayment
        {
            CampusId = v.CampusId, ReceiptNo = $"{campus?.Code ?? "C"}-R-{no:000000}", Amount = amount, PaidOn = r.PaidOn, Method = r.Method,
            Reference = string.IsNullOrWhiteSpace(r.Reference) ? null : r.Reference.Trim(), ReceivedByUserId = currentUser.UserId ?? 0,
            AddedLateFine = addedFine,
        };
        v.Payments.Add(payment);
        VoucherLedger.Recompute(v);
        await repositories.SaveChangesAsync(ct);
        return (await repositories.Vouchers.PaymentDtoAsync(payment.Id, ct))!;
    }

    public async Task<Result<PaymentDto>> VoidAsync(int id, string? reason, CancellationToken ct = default)
    {
        var text = reason?.Trim() ?? "";
        if (text.Length is 0 or > 200) return Error.Validation("Give a reason (200 characters at most).");
        var p = await repositories.Vouchers.GetPaymentTrackedAsync(id, ct);
        if (p == null) return Error.NotFound("Payment");
        if (p.IsVoid) return Error.Validation("This payment is already void.");
        if (p.FeeVoucher.Status is VoucherStatus.CarriedForward or VoucherStatus.Cancelled) return VoucherService.Closed;
        // Reopening a challan while a later one exists would leave the student with two open challans.
        if (await repositories.Vouchers.HasLaterAsync(p.FeeVoucher.StudentId, p.FeeVoucher.Month, ct))
            return Error.Validation("A later challan exists. Cancel it first.");
        p.IsVoid = true;
        p.VoidReason = text;
        p.VoidedOn = DateTime.UtcNow;
        p.VoidedByUserId = currentUser.UserId;
        VoucherLedger.Recompute(p.FeeVoucher);
        await repositories.SaveChangesAsync(ct);
        return (await repositories.Vouchers.PaymentDtoAsync(id, ct))!;
    }

    public async Task<Result<ReceiptDto>> ReceiptAsync(int id, CancellationToken ct = default)
    {
        var payment = await repositories.Vouchers.PaymentDtoAsync(id, ct);
        if (payment == null) return Error.NotFound("Payment");
        var voucher = (await repositories.Vouchers.DtoAsync(payment.VoucherId, ct))!;
        var meta = (await repositories.Vouchers.PrintMetaAsync([payment.VoucherId], ct))[payment.VoucherId];
        var school = await repositories.Schools.GetTrackedAsync(ct);
        return new ReceiptDto(school?.Name ?? "", meta.CampusName, payment, voucher, voucher.StudentName,
            $"{voucher.ClassName} - {voucher.SectionName}", voucher.Balance);
    }

    public async Task<Result<IReadOnlyList<PaymentDto>>> TodayAsync(CancellationToken ct = default)
    {
        if (currentUser.ActiveCampusId == null) return Error.Validation("Select a campus.");
        return await repositories.Vouchers.PaymentsOnAsync(Today, ct);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
}
