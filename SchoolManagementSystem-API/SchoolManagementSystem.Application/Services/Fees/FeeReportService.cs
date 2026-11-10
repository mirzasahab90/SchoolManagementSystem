using System.Globalization;
using System.Text;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.Entities.Fees;

namespace SchoolManagementSystem.Application.Services.Fees;

/// <summary>Defaulters, collection / monthly / head-wise reports, CSV exports, the student Fees tab and dashboard numbers.</summary>
public class FeeReportService(SchoolManagementSystemRepositories repositories, ICurrentUser currentUser) : IFeeReportService
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public async Task<Result<DefaultersDto>> DefaultersAsync(DefaulterQuery q, CancellationToken ct = default)
    {
        var all = await AllDefaultersAsync(q, ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 500);
        return new DefaultersDto(all.Skip((page - 1) * size).Take(size).ToList(), all.Count, all.Sum(d => d.Balance));
    }

    public async Task<Result<(byte[] Content, string FileName)>> DefaultersCsvAsync(DefaulterQuery q, CancellationToken ct = default)
    {
        var all = await AllDefaultersAsync(q, ct);
        var csv = new StringBuilder("Admission no,Name,Class,Section,Guardian phone,Months,Balance\n");
        foreach (var d in all)
            csv.Append(string.Join(",", Cell(d.AdmissionNo), Cell(d.Name + (d.StudentLeft ? " (Left)" : "")), Cell(d.ClassName), Cell(d.SectionName),
                Cell(d.GuardianPhone), d.Months, Money(d.Balance))).Append('\n');
        return (Utf8(csv), $"defaulters-{Today:yyyy-MM-dd}.csv");
    }

    public async Task<Result<CollectionReportDto>> CollectionAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from) return Error.Validation("The end date is before the start date.");
        if (to.DayNumber - from.DayNumber > 366) return Error.Validation("Pick at most a year.");
        var payments = await repositories.FeeReports.PaymentsBetweenAsync(from, to, ct);
        var live = payments.Where(p => !p.IsVoid).ToList();
        var ids = payments.Select(p => p.Id).ToList();
        var dtos = await repositories.Vouchers.PaymentDtosByIdsAsync(ids, ct);
        var names = dtos.ToDictionary(d => d.Id, d => d.ReceivedBy);
        var byMethod = live.GroupBy(p => p.Method).OrderBy(g => g.Key)
            .Select(g => new MethodTotal(g.Key, g.Count(), g.Sum(p => p.Amount))).ToList();
        var byUser = live.GroupBy(p => names.GetValueOrDefault(p.Id) ?? "").OrderBy(g => g.Key)
            .Select(g => new UserTotal(g.Key, g.Count(), g.Sum(p => p.Amount))).ToList();
        IReadOnlyList<CampusTotalDto>? campuses = null;
        if (currentUser.ActiveCampusId == null)
            campuses = (await repositories.FeeReports.CampusesAsync(ct))
                .Select(c => new CampusTotalDto(c.Code, c.Name, 0, live.Where(p => p.CampusId == c.Id).Sum(p => p.Amount), 0))
                .Where(c => c.Collected > 0).ToList();
        return new CollectionReportDto(from, to, dtos.OrderByDescending(p => p.Id).ToList(), byMethod, byUser, live.Sum(p => p.Amount),
            payments.Where(p => p.IsVoid).Sum(p => p.Amount), campuses);
    }

    public async Task<Result<(byte[] Content, string FileName)>> CollectionCsvAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var r = await CollectionAsync(from, to, ct);
        if (!r.IsSuccess) return r.Error!;
        var csv = new StringBuilder("Receipt no,Challan no,Date,Method,Reference,Amount,Received by,Void\n");
        foreach (var p in r.Value!.Payments)
            csv.Append(string.Join(",", Cell(p.ReceiptNo), Cell(p.VoucherNo), p.PaidOn.ToString("yyyy-MM-dd"), p.Method, Cell(p.Reference),
                Money(p.Amount), Cell(p.ReceivedBy), p.IsVoid ? "Yes" : "No")).Append('\n');
        return (Utf8(csv), $"collection-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.csv");
    }

    public async Task<Result<MonthlyReportDto>> MonthlyAsync(DateOnly month, CancellationToken ct = default)
    {
        var m = new DateOnly(month.Year, month.Month, 1);
        var rows = await repositories.FeeReports.MonthAsync(m, ct);
        var byClass = rows.GroupBy(r => (r.ClassName, r.DisplayOrder)).OrderBy(g => g.Key.DisplayOrder).ThenBy(g => g.Key.ClassName)
            .Select(g => Row(g.Key.ClassName, g.ToList())).ToList();
        IReadOnlyList<CampusTotalDto>? campuses = null;
        if (currentUser.ActiveCampusId == null)
            campuses = (await repositories.FeeReports.CampusesAsync(ct))
                .Select(c => (c, rows.Where(r => r.CampusId == c.Id).ToList())).Where(x => x.Item2.Count > 0)
                .Select(x => { var t = Row("", x.Item2); return new CampusTotalDto(x.c.Code, x.c.Name, t.Billed, t.Collected, t.Outstanding); })
                .ToList();
        return new MonthlyReportDto(m, byClass, Row("Total", rows), campuses);
    }

    public async Task<Result<(byte[] Content, string FileName)>> MonthlyCsvAsync(DateOnly month, CancellationToken ct = default)
    {
        var r = (await MonthlyAsync(month, ct)).Value!;
        var csv = new StringBuilder("Class,Challans,Billed,Concession,Collected,Outstanding,Collection %\n");
        foreach (var x in r.Rows.Append(r.Totals))
            csv.Append(string.Join(",", Cell(x.ClassName), x.Vouchers, Money(x.Billed), Money(x.Concession), Money(x.Collected), Money(x.Outstanding),
                x.CollectionPercent.ToString("0.##", CultureInfo.InvariantCulture))).Append('\n');
        return (Utf8(csv), $"fees-{r.Month:yyyy-MM}.csv");
    }

    public async Task<Result<HeadReportDto>> HeadsAsync(DateOnly month, CancellationToken ct = default)
    {
        var m = new DateOnly(month.Year, month.Month, 1);
        return new HeadReportDto(m, await repositories.FeeReports.HeadsAsync(m, ct));
    }

    public async Task<Result<(byte[] Content, string FileName)>> HeadsCsvAsync(DateOnly month, CancellationToken ct = default)
    {
        var r = (await HeadsAsync(month, ct)).Value!;
        var csv = new StringBuilder("Head,Amount,Concession,Net\n");
        foreach (var x in r.Rows) csv.Append(string.Join(",", Cell(x.Head), Money(x.Amount), Money(x.Concession), Money(x.Net))).Append('\n');
        return (Utf8(csv), $"fee-heads-{r.Month:yyyy-MM}.csv");
    }

    public async Task<Result<StudentFeesDto>> StudentFeesAsync(int studentId, CancellationToken ct = default)
    {
        if (await repositories.Students.GetTrackedAsync(studentId, ct) == null) return Error.NotFound("Student");
        var ids = await repositories.FeeReports.StudentVoucherIdsAsync(studentId, ct);
        var vouchers = (await repositories.Vouchers.DtosAsync(ids, ct)).OrderBy(v => ids.IndexOf(v.Id)).ToList();
        var payments = new List<PaymentDto>();
        foreach (var id in ids) payments.AddRange(await repositories.Vouchers.PaymentsAsync(id, ct));
        var balance = vouchers.Where(v => v.Status is VoucherStatus.Unpaid or VoucherStatus.PartiallyPaid).Sum(v => v.Balance);
        return new StudentFeesDto(balance, vouchers, payments.OrderByDescending(p => p.Id).ToList());
    }

    /// <summary>(collected this month, outstanding now) for the dashboard.</summary>
    public async Task<(decimal Collected, decimal Outstanding)> DashboardAsync(CancellationToken ct = default)
    {
        var first = new DateOnly(Today.Year, Today.Month, 1);
        return (await repositories.FeeReports.CollectedBetweenAsync(first, Today, ct), await repositories.FeeReports.OutstandingAsync(ct));
    }

    // ---------------------------------------------------------------- helpers

    private async Task<List<DefaulterDto>> AllDefaultersAsync(DefaulterQuery q, CancellationToken ct)
    {
        var open = await repositories.FeeReports.PastDueAsync(Today, q.ClassId, q.SectionId, ct);
        var chains = await repositories.FeeReports.ChainsAsync(open.Select(o => o.StudentId).Distinct().ToList(), ct);
        var into = chains.Where(c => c.To != null).ToLookup(c => c.To!.Value);
        var list = new List<DefaulterDto>();
        foreach (var o in open)
        {
            int months = 1;
            var oldest = o.Month;
            var frontier = new Queue<int>([o.VoucherId]);
            var seen = new HashSet<int> { o.VoucherId };
            while (frontier.Count > 0)
                foreach (var prev in into[frontier.Dequeue()])
                {
                    if (!seen.Add(prev.Id)) continue;
                    months++;
                    if (prev.Month < oldest) oldest = prev.Month;
                    frontier.Enqueue(prev.Id);
                }

            list.Add(new DefaulterDto(o.StudentId, o.Name, o.AdmissionNo, o.ClassName, o.SectionName, o.GuardianPhone, months, o.Balance, oldest,
                o.Left, o.VoucherId));
        }

        return list.Where(d => d.Balance > 0 && (q.MinMonths == null || d.Months >= q.MinMonths) && (q.MinAmount == null || d.Balance >= q.MinAmount))
            .OrderByDescending(d => d.Months).ThenByDescending(d => d.Balance).ThenBy(d => d.Name).ToList();
    }

    private static MonthlyClassRow Row(string name, List<FeeReportRepository.MonthRow> rows)
    {
        var billed = rows.Sum(r => r.Billed);
        var concession = rows.Sum(r => r.Concession);
        var collected = rows.Sum(r => r.Collected);
        var net = billed - concession;
        var pct = net <= 0 ? 0 : decimal.Round(collected * 100 / net, 2, MidpointRounding.AwayFromZero);
        // Outstanding is what is still open on these challans; a carried balance counts on the challan it moved to.
        return new MonthlyClassRow(name, rows.Count, billed, concession, collected, rows.Sum(r => r.Open), pct);
    }

    private static string Money(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Cell(string? v)
    {
        var s = v ?? "";
        if (s.Length > 0 && "=+-@".Contains(s[0])) s = "'" + s;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    private static byte[] Utf8(StringBuilder sb) => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
}
