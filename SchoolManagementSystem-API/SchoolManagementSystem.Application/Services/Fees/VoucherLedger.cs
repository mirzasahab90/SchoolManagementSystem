using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>The one place a challan's totals and status change: carry-forward, late fine, recompute.</summary>
public static class VoucherLedger
{
    /// <summary>Fine the challan would add if paid (or carried) on this date.</summary>
    public static decimal FineOn(FeeVoucher v, DateOnly date) =>
        v.IsOpen && !v.LateFineApplied && date > v.DueDate ? v.LateFineAmount : 0;

    /// <summary>Arrears the next challan takes from this one: balance plus the fine once the due date has passed.</summary>
    public static decimal ArrearsOf(FeeVoucher from, DateOnly today) => from.Balance + FineOn(from, today);

    /// <summary>
    /// Moves the open balance of <paramref name="from"/> into <paramref name="to"/> as an Arrears line and closes it.
    /// When the fine goes into the arrears it counts as applied, so it is never charged twice. The caller links
    /// <see cref="FeeVoucher.CarriedToVoucherId"/> once <paramref name="to"/> has an id.
    /// </summary>
    public static void CarryForward(FeeVoucher from, FeeVoucher to, DateOnly today)
    {
        var fine = FineOn(from, today);
        var amount = from.Balance + fine;
        if (fine > 0) from.LateFineApplied = true;
        to.Lines.Add(new FeeVoucherLine
        {
            Kind = VoucherLineKind.Arrears, Description = $"Arrears ({from.Month:MMM yyyy})", Amount = amount, Net = amount,
            FromVoucherId = from.Id,
        });
        from.Status = VoucherStatus.CarriedForward;
        Recompute(to);
    }

    /// <summary>Adds the late fine line (once) when paying after the due date. Returns true when it was added.</summary>
    public static bool ApplyLateFine(FeeVoucher v, DateOnly paidOn)
    {
        var fine = FineOn(v, paidOn);
        if (fine <= 0) return false;
        v.Lines.Add(new FeeVoucherLine { Kind = VoucherLineKind.LateFine, Description = "Late fine", Amount = fine, Net = fine });
        v.LateFineApplied = true;
        Recompute(v);
        return true;
    }

    /// <summary>Total from the lines, Paid from non-void payments, status from the balance (CarriedForward / Cancelled stay).</summary>
    public static void Recompute(FeeVoucher v)
    {
        v.Total = v.Lines.Sum(l => l.Net);
        v.Paid = v.Payments.Where(p => !p.IsVoid && !p.IsDeleted).Sum(p => p.Amount);
        if (v.Status is VoucherStatus.CarriedForward or VoucherStatus.Cancelled) return;
        v.Status = v.Paid >= v.Total ? VoucherStatus.Paid : v.Paid > 0 ? VoucherStatus.PartiallyPaid : VoucherStatus.Unpaid;
    }

    /// <summary>Status an open challan returns to (used when a carry-forward is undone).</summary>
    public static void Reopen(FeeVoucher v)
    {
        v.Status = VoucherStatus.Unpaid;
        v.CarriedToVoucherId = null;
        Recompute(v);
    }
}
