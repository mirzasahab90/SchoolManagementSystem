using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.DTOs.Fees;
using SchoolManagementSystem.Domain.DTOs.Students;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Hiring;
using SchoolManagementSystem.Domain.Entities.Students;
using SchoolManagementSystem.Domain.Entities.Fees;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

/// <summary>Fee heads, class-wise amounts and fee settings (CampusId null = Main Campus).</summary>
public class FeeSetupRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>Main Campus heads plus the campus's own (Main Campus only when campusId is null).</summary>
    public Task<List<FeeHead>> HeadsAsync(int? campusId, CancellationToken ct = default) =>
        Db.FeeHeads.IgnoreQueryFilters().AsNoTracking()
            .Where(h => !h.IsDeleted && (h.CampusId == null || h.CampusId == campusId))
            .OrderBy(h => h.SortOrder).ThenBy(h => h.Name).ToListAsync(ct);

    public Task<FeeHead?> GetHeadTrackedAsync(int id, CancellationToken ct = default) =>
        Db.FeeHeads.FirstOrDefaultAsync(h => h.Id == id, ct);

    public Task<bool> HeadNameTakenAsync(int? campusId, string name, int? excludeId, CancellationToken ct = default) =>
        Db.FeeHeads.IgnoreQueryFilters().AnyAsync(h => !h.IsDeleted && h.CampusId == campusId && h.Name == name
                                                       && (excludeId == null || h.Id != excludeId), ct);

    public async Task<bool> HeadInUseAsync(int headId, CancellationToken ct = default) =>
        await Db.FeeStructures.IgnoreQueryFilters().AnyAsync(s => !s.IsDeleted && s.FeeHeadId == headId, ct)
        || await Db.FeeVoucherLines.AnyAsync(l => l.FeeHeadId == headId, ct)
        || await Db.StudentConcessions.IgnoreQueryFilters().AnyAsync(c => !c.IsDeleted && c.FeeHeadId == headId, ct);

    /// <summary>Main Campus classes plus the campus's own, in promotion order.</summary>
    public Task<List<SchoolClass>> ClassesAsync(int? campusId, CancellationToken ct = default) =>
        Db.Classes.IgnoreQueryFilters().AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive && (c.CampusId == null || c.CampusId == campusId))
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(ct);

    /// <summary>Main Campus rows and the campus's rows for a session.</summary>
    public Task<List<FeeStructure>> StructureAsync(int? campusId, int sessionId, CancellationToken ct = default) =>
        Db.FeeStructures.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.AcademicSessionId == sessionId && (s.CampusId == null || s.CampusId == campusId))
            .ToListAsync(ct);

    public Task<List<FeeStructure>> OwnedStructureTrackedAsync(int? campusId, int sessionId, CancellationToken ct = default) =>
        Db.FeeStructures.IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && s.AcademicSessionId == sessionId && s.CampusId == campusId).ToListAsync(ct);

    /// <summary>Main Campus settings row and the campus row (either may be null).</summary>
    public async Task<(FeeSettings? Main, FeeSettings? Campus)> SettingsAsync(int? campusId, CancellationToken ct = default)
    {
        var rows = await Db.FeeSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && (s.CampusId == null || s.CampusId == campusId)).ToListAsync(ct);
        return (rows.FirstOrDefault(s => s.CampusId == null), campusId == null ? null : rows.FirstOrDefault(s => s.CampusId == campusId));
    }

    public Task<FeeSettings?> OwnedSettingsTrackedAsync(int? campusId, CancellationToken ct = default) =>
        Db.FeeSettings.IgnoreQueryFilters().FirstOrDefaultAsync(s => !s.IsDeleted && s.CampusId == campusId, ct);
}

/// <summary>Challans, their lines and payments (campus-scoped). Lists join live students only.</summary>
public class VoucherRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    /// <summary>Active enrollments of active students in a session, for one campus (optionally one class / section).</summary>
    public Task<List<Enrollment>> RosterAsync(int campusId, int sessionId, int? classId, int? sectionId, CancellationToken ct = default) =>
        Db.Enrollments.AsNoTracking().Include(e => e.Student).Include(e => e.Section).ThenInclude(s => s.Class)
            .Where(e => e.CampusId == campusId && e.AcademicSessionId == sessionId && e.Status == EnrollmentStatus.Active
                        && e.Student.Status == StudentStatus.Active
                        && (classId == null || e.Section.ClassId == classId) && (sectionId == null || e.SectionId == sectionId))
            .OrderBy(e => e.Section.Class.DisplayOrder).ThenBy(e => e.Section.Name).ThenBy(e => e.RollNo)
            .ToListAsync(ct);

    public async Task<HashSet<int>> StudentsWithVoucherAsync(IReadOnlyCollection<int> studentIds, DateOnly month, CancellationToken ct = default) =>
        (await Db.FeeVouchers.IgnoreQueryFilters()
            .Where(v => !v.IsDeleted && v.Month == month && v.Status != VoucherStatus.Cancelled && studentIds.Contains(v.StudentId))
            .Select(v => v.StudentId).ToListAsync(ct)).ToHashSet();

    public async Task<HashSet<int>> StudentsWithLaterAsync(IReadOnlyCollection<int> studentIds, DateOnly month, CancellationToken ct = default) =>
        (await Db.FeeVouchers.IgnoreQueryFilters()
            .Where(v => !v.IsDeleted && v.Month > month && v.Status != VoucherStatus.Cancelled && studentIds.Contains(v.StudentId))
            .Select(v => v.StudentId).ToListAsync(ct)).ToHashSet();

    /// <summary>The latest open challan per student from an earlier month (tracked, with lines and payments).</summary>
    public async Task<Dictionary<int, FeeVoucher>> OpenBeforeAsync(IReadOnlyCollection<int> studentIds, DateOnly month, CancellationToken ct = default)
    {
        var list = await Db.FeeVouchers.Include(v => v.Lines).Include(v => v.Payments).AsSplitQuery()
            .Where(v => studentIds.Contains(v.StudentId) && v.Month < month
                        && (v.Status == VoucherStatus.Unpaid || v.Status == VoucherStatus.PartiallyPaid))
            .ToListAsync(ct);
        return list.GroupBy(v => v.StudentId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Month).First());
    }

    public Task<FeeVoucher?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.FeeVouchers.Include(v => v.Lines).Include(v => v.Payments).AsSplitQuery().FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<List<FeeVoucher>> CarriedIntoTrackedAsync(int voucherId, CancellationToken ct = default) =>
        Db.FeeVouchers.Include(v => v.Lines).Include(v => v.Payments).AsSplitQuery()
            .Where(v => v.CarriedToVoucherId == voucherId).ToListAsync(ct);

    public Task<bool> HasLaterAsync(int studentId, DateOnly month, CancellationToken ct = default) =>
        Db.FeeVouchers.AnyAsync(v => v.StudentId == studentId && v.Month > month && v.Status != VoucherStatus.Cancelled, ct);

    private IQueryable<VoucherDto> Project(IQueryable<FeeVoucher> q) =>
        q.Select(v => new VoucherDto(
            v.Id, v.VoucherNo, v.StudentId, v.Student.FirstName + " " + v.Student.LastName, v.Student.AdmissionNo,
            v.Enrollment.Section.Class.Name, v.Enrollment.Section.Name, v.Enrollment.RollNo, v.Month, v.IssueDate, v.DueDate, v.Total,
            v.LateFineAmount, v.LateFineApplied, v.Paid, v.Total - v.Paid,
            v.Total - v.Paid + ((v.Status == VoucherStatus.Unpaid || v.Status == VoucherStatus.PartiallyPaid) && !v.LateFineApplied ? v.LateFineAmount : 0),
            v.Status));

    public async Task<PagedResult<VoucherDto>> ListAsync(VoucherQuery query, CancellationToken ct = default)
    {
        var q = Db.FeeVouchers.AsNoTracking().Where(v => !v.Student.IsDeleted);
        if (query.Month is DateOnly m) q = q.Where(v => v.Month == m);
        if (query.ClassId is int c) q = q.Where(v => v.Enrollment.Section.ClassId == c);
        if (query.SectionId is int s) q = q.Where(v => v.Enrollment.SectionId == s);
        if (query.Status is VoucherStatus st) q = q.Where(v => v.Status == st);
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var t = query.Q.Trim();
            q = q.Where(v => v.VoucherNo.Contains(t) || v.Student.AdmissionNo.Contains(t) || (v.Student.FirstName + " " + v.Student.LastName).Contains(t));
        }

        var total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(v => v.Month).ThenBy(v => v.Enrollment.Section.Class.DisplayOrder)
                .ThenBy(v => v.Enrollment.Section.Name).ThenBy(v => v.Enrollment.RollNo).ThenBy(v => v.Id))
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items, total);
    }

    public Task<VoucherDto?> DtoAsync(int id, CancellationToken ct = default) =>
        Project(Db.FeeVouchers.AsNoTracking().Where(v => v.Id == id)).FirstOrDefaultAsync(ct);

    public Task<List<VoucherDto>> DtosAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        Project(Db.FeeVouchers.AsNoTracking().Where(v => ids.Contains(v.Id))).ToListAsync(ct);

    public Task<List<VoucherLineDto>> LinesAsync(int voucherId, CancellationToken ct = default) =>
        Db.FeeVoucherLines.AsNoTracking().Where(l => l.FeeVoucherId == voucherId).OrderBy(l => l.Id)
            .Select(l => new VoucherLineDto(l.Id, l.Kind, l.Description, l.Amount, l.Concession, l.ConcessionNote, l.Net)).ToListAsync(ct);

    public IQueryable<PaymentDto> PaymentDtos(IQueryable<FeePayment> q) =>
        q.Select(p => new PaymentDto(p.Id, p.ReceiptNo, p.FeeVoucherId, p.FeeVoucher.VoucherNo, p.Amount, p.PaidOn, p.Method, p.Reference,
            Db.Users.Where(u => u.Id == p.ReceivedByUserId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault() ?? "",
            p.IsVoid, p.VoidReason));

    public Task<List<PaymentDto>> PaymentsAsync(int voucherId, CancellationToken ct = default) =>
        PaymentDtos(Db.FeePayments.AsNoTracking().Where(p => p.FeeVoucherId == voucherId).OrderBy(p => p.Id)).ToListAsync(ct);

    public sealed record PrintMeta(int CampusId, string CampusName, string? CampusAddress, string? CampusPhone, string? GuardianName);

    public async Task<Dictionary<int, PrintMeta>> PrintMetaAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        var rows = await Db.FeeVouchers.AsNoTracking().Where(v => ids.Contains(v.Id))
            .Select(v => new
            {
                v.Id, v.CampusId,
                Campus = Db.Campuses.Where(c => c.Id == v.CampusId).Select(c => new { c.Name, c.Address, c.City, c.Phone }).FirstOrDefault(),
                Guardian = v.Student.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.FullName).FirstOrDefault(),
            }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => new PrintMeta(r.CampusId, r.Campus?.Name ?? "",
            string.Join(", ", new[] { r.Campus?.Address, r.Campus?.City }.Where(x => !string.IsNullOrWhiteSpace(x))), r.Campus?.Phone, r.Guardian));
    }

    public Task<FeePayment?> GetPaymentTrackedAsync(int id, CancellationToken ct = default) =>
        Db.FeePayments.Include(p => p.FeeVoucher).ThenInclude(v => v.Lines)
            .Include(p => p.FeeVoucher).ThenInclude(v => v.Payments).AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<PaymentDto>> PaymentDtosByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        PaymentDtos(Db.FeePayments.AsNoTracking().Where(p => ids.Contains(p.Id))).ToListAsync(ct);

    public Task<PaymentDto?> PaymentDtoAsync(int id, CancellationToken ct = default) =>
        PaymentDtos(Db.FeePayments.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct);

    public Task<List<PaymentDto>> PaymentsOnAsync(DateOnly day, CancellationToken ct = default) =>
        PaymentDtos(Db.FeePayments.AsNoTracking().Where(p => p.PaidOn == day).OrderByDescending(p => p.Id)).ToListAsync(ct);

    /// <summary>Students for the collect screen: exact admission / challan number first, else name contains (max 10).</summary>
    public async Task<List<int>> LookupStudentIdsAsync(string q, CancellationToken ct = default)
    {
        var t = q.Trim();
        var byVoucher = await Db.FeeVouchers.AsNoTracking().Where(v => v.VoucherNo == t).Select(v => v.StudentId).ToListAsync(ct);
        if (byVoucher.Count > 0) return byVoucher.Distinct().ToList();
        var exact = await Db.Students.AsNoTracking().Where(s => s.AdmissionNo == t).Select(s => s.Id).ToListAsync(ct);
        if (exact.Count > 0) return exact;
        return await Db.Students.AsNoTracking()
            .Where(s => (s.FirstName + " " + s.LastName).Contains(t) || s.AdmissionNo.Contains(t))
            .OrderBy(s => s.Status).ThenBy(s => s.FirstName).Select(s => s.Id).Take(10).ToListAsync(ct);
    }

    public sealed record LookupStudent(
        int Id, string Name, string AdmissionNo, string? ClassName, string? SectionName, string? GuardianName, string? GuardianPhone, bool Left,
        int? OpenVoucherId);

    public async Task<List<LookupStudent>> LookupStudentsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default)
    {
        var rows = await Db.Students.AsNoTracking().Where(s => ids.Contains(s.Id))
            .Select(s => new LookupStudent(
                s.Id, s.FirstName + " " + s.LastName, s.AdmissionNo,
                Db.Enrollments.Where(e => e.StudentId == s.Id).OrderByDescending(e => e.Status == EnrollmentStatus.Active).ThenByDescending(e => e.Id)
                    .Select(e => e.Section.Class.Name).FirstOrDefault(),
                Db.Enrollments.Where(e => e.StudentId == s.Id).OrderByDescending(e => e.Status == EnrollmentStatus.Active).ThenByDescending(e => e.Id)
                    .Select(e => e.Section.Name).FirstOrDefault(),
                s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.FullName).FirstOrDefault(),
                s.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.Phone).FirstOrDefault(),
                s.Status == StudentStatus.Left,
                Db.FeeVouchers.Where(v => v.StudentId == s.Id && (v.Status == VoucherStatus.Unpaid || v.Status == VoucherStatus.PartiallyPaid))
                    .OrderByDescending(v => v.Month).Select(v => (int?)v.Id).FirstOrDefault()))
            .ToListAsync(ct);
        return ids.Select(id => rows.FirstOrDefault(r => r.Id == id)).Where(r => r != null).Select(r => r!).ToList();
    }
}

/// <summary>Read models for defaulters, reports, the student Fees tab and the dashboard (campus-scoped, live students only).</summary>
public class FeeReportRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public sealed record OpenRow(
        int VoucherId, int StudentId, string Name, string AdmissionNo, string ClassName, int ClassId, string SectionName, int SectionId,
        string? GuardianPhone, bool Left, decimal Balance, DateOnly Month, DateOnly DueDate);

    /// <summary>Open challans whose due date has passed.</summary>
    public Task<List<OpenRow>> PastDueAsync(DateOnly today, int? classId, int? sectionId, CancellationToken ct = default) =>
        Db.FeeVouchers.AsNoTracking()
            .Where(v => !v.Student.IsDeleted && v.DueDate < today
                        && (v.Status == VoucherStatus.Unpaid || v.Status == VoucherStatus.PartiallyPaid)
                        && (classId == null || v.Enrollment.Section.ClassId == classId) && (sectionId == null || v.Enrollment.SectionId == sectionId))
            .Select(v => new OpenRow(v.Id, v.StudentId, v.Student.FirstName + " " + v.Student.LastName, v.Student.AdmissionNo,
                v.Enrollment.Section.Class.Name, v.Enrollment.Section.ClassId, v.Enrollment.Section.Name, v.Enrollment.SectionId,
                v.Student.Guardians.OrderByDescending(g => g.IsPrimary).Select(g => g.Guardian.Phone).FirstOrDefault(),
                v.Student.Status == StudentStatus.Left, v.Total - v.Paid, v.Month, v.DueDate))
            .ToListAsync(ct);

    /// <summary>(Id, CarriedToVoucherId, Month) of every challan of these students, to walk carry chains.</summary>
    public async Task<List<(int Id, int? To, DateOnly Month)>> ChainsAsync(IReadOnlyCollection<int> studentIds, CancellationToken ct = default) =>
        (await Db.FeeVouchers.AsNoTracking().Where(v => studentIds.Contains(v.StudentId) && v.Status == VoucherStatus.CarriedForward)
            .Select(v => new { v.Id, v.CarriedToVoucherId, v.Month }).ToListAsync(ct))
        .Select(x => (x.Id, x.CarriedToVoucherId, x.Month)).ToList();

    public sealed record MonthRow(int CampusId, string ClassName, int DisplayOrder, decimal Billed, decimal Concession, decimal Collected, decimal Open);

    /// <summary>One row per non-cancelled challan of the month.</summary>
    public Task<List<MonthRow>> MonthAsync(DateOnly month, CancellationToken ct = default) =>
        Db.FeeVouchers.AsNoTracking()
            .Where(v => !v.Student.IsDeleted && v.Month == month && v.Status != VoucherStatus.Cancelled)
            .Select(v => new MonthRow(v.CampusId, v.Enrollment.Section.Class.Name, v.Enrollment.Section.Class.DisplayOrder,
                v.Lines.Where(l => l.Kind == VoucherLineKind.Head).Sum(l => l.Amount),
                v.Lines.Where(l => l.Kind == VoucherLineKind.Head).Sum(l => l.Concession),
                v.Payments.Where(p => !p.IsVoid && !p.IsDeleted).Sum(p => p.Amount),
                v.Status == VoucherStatus.Unpaid || v.Status == VoucherStatus.PartiallyPaid ? v.Total - v.Paid : 0))
            .ToListAsync(ct);

    public async Task<List<HeadRow>> HeadsAsync(DateOnly month, CancellationToken ct = default)
    {
        var ids = await Db.FeeVouchers.AsNoTracking()
            .Where(v => !v.Student.IsDeleted && v.Month == month && v.Status != VoucherStatus.Cancelled).Select(v => v.Id).ToListAsync(ct);
        var lines = await Db.FeeVoucherLines.AsNoTracking()
            .Where(l => l.Kind == VoucherLineKind.Head && ids.Contains(l.FeeVoucherId))
            .Select(l => new { l.Description, l.Amount, l.Concession, l.Net }).ToListAsync(ct);
        return lines.GroupBy(l => l.Description).OrderBy(g => g.Key)
            .Select(g => new HeadRow(g.Key, g.Sum(l => l.Amount), g.Sum(l => l.Concession), g.Sum(l => l.Net))).ToList();
    }

    public Task<List<FeePayment>> PaymentsBetweenAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.FeePayments.AsNoTracking().Where(p => p.PaidOn >= from && p.PaidOn <= to).ToListAsync(ct);

    public Task<decimal> OutstandingAsync(CancellationToken ct = default) =>
        Db.FeeVouchers.Where(v => !v.Student.IsDeleted && (v.Status == VoucherStatus.Unpaid || v.Status == VoucherStatus.PartiallyPaid))
            .SumAsync(v => v.Total - v.Paid, ct);

    public Task<decimal> CollectedBetweenAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        Db.FeePayments.Where(p => !p.IsVoid && p.PaidOn >= from && p.PaidOn <= to).SumAsync(p => p.Amount, ct);

    public async Task<List<(int Id, string Code, string Name)>> CampusesAsync(CancellationToken ct = default) =>
        (await Db.Campuses.AsNoTracking().OrderBy(c => c.Code).Select(c => new { c.Id, c.Code, c.Name }).ToListAsync(ct))
        .Select(c => (c.Id, c.Code, c.Name)).ToList();

    public Task<List<int>> StudentVoucherIdsAsync(int studentId, CancellationToken ct = default) =>
        Db.FeeVouchers.AsNoTracking().Where(v => v.StudentId == studentId).OrderByDescending(v => v.Month).ThenByDescending(v => v.Id)
            .Select(v => v.Id).ToListAsync(ct);
}

/// <summary>Student concessions, sibling order and staff-child matches (campus-scoped).</summary>
public class ConcessionRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public async Task<PagedResult<ConcessionDto>> ListAsync(int sessionId, string? q, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Db.StudentConcessions.AsNoTracking().Where(c => c.AcademicSessionId == sessionId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            query = query.Where(c => (c.Student.FirstName + " " + c.Student.LastName).Contains(t) || c.Student.AdmissionNo.Contains(t)
                                     || c.Reason.Contains(t));
        }

        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(c => c.Student.FirstName).ThenBy(c => c.Student.LastName).ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new
            {
                c.Id, c.StudentId, Name = c.Student.FirstName + " " + c.Student.LastName, c.Student.AdmissionNo, c.AcademicSessionId,
                c.FeeHeadId, HeadName = c.FeeHead != null ? c.FeeHead.Name : null, c.Kind, c.StaffId, c.ValueType, c.Value, c.Reason, c.IsActive,
                ClassName = Db.Enrollments.Where(e => e.StudentId == c.StudentId && e.AcademicSessionId == c.AcademicSessionId)
                    .OrderByDescending(e => e.Id).Select(e => e.Section.Class.Name).FirstOrDefault(),
                SectionName = Db.Enrollments.Where(e => e.StudentId == c.StudentId && e.AcademicSessionId == c.AcademicSessionId)
                    .OrderByDescending(e => e.Id).Select(e => e.Section.Name).FirstOrDefault(),
                StaffName = Db.Staff.Where(s => s.Id == c.StaffId).Select(s => s.FirstName + " " + s.LastName).FirstOrDefault(),
            }).ToListAsync(ct);

        return new(rows.Select(r => new ConcessionDto(
            r.Id, r.StudentId, r.Name, r.AdmissionNo, r.ClassName, r.SectionName, r.AcademicSessionId, r.FeeHeadId,
            r.HeadName ?? "All monthly heads", r.Kind, r.StaffId, r.StaffName, r.ValueType, r.Value, r.Reason, r.IsActive)).ToList(), total);
    }

    public Task<StudentConcession?> GetTrackedAsync(int id, CancellationToken ct = default) =>
        Db.StudentConcessions.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> DuplicateAsync(int studentId, int sessionId, int? headId, ConcessionKind kind, int? excludeId, CancellationToken ct = default) =>
        Db.StudentConcessions.IgnoreQueryFilters().AnyAsync(c => !c.IsDeleted && c.IsActive && c.StudentId == studentId
                                                                && c.AcademicSessionId == sessionId && c.FeeHeadId == headId && c.Kind == kind
                                                                && (excludeId == null || c.Id != excludeId), ct);

    public Task<List<StudentConcession>> ActiveForStudentAsync(int studentId, int sessionId, CancellationToken ct = default) =>
        Db.StudentConcessions.IgnoreQueryFilters().AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive && c.StudentId == studentId && c.AcademicSessionId == sessionId).ToListAsync(ct);

    /// <summary>
    /// 1-based place of the student among active students of the same campus who share any guardian and have an active
    /// enrollment in the session, ordered by admission number.
    /// </summary>
    public async Task<int> SiblingPositionAsync(int studentId, int campusId, int sessionId, CancellationToken ct = default)
    {
        var guardianIds = Db.StudentGuardians.Where(g => g.StudentId == studentId).Select(g => g.GuardianId);
        var family = await Db.Students.IgnoreQueryFilters().AsNoTracking()
            .Where(s => !s.IsDeleted && s.CampusId == campusId && s.Status == StudentStatus.Active
                        && (s.Id == studentId || s.Guardians.Any(g => guardianIds.Contains(g.GuardianId)))
                        && Db.Enrollments.IgnoreQueryFilters().Any(e => !e.IsDeleted && e.StudentId == s.Id && e.AcademicSessionId == sessionId
                                                                       && e.Status == EnrollmentStatus.Active))
            .Select(s => new { s.Id, s.AdmissionNo }).ToListAsync(ct);
        var ordered = family.OrderBy(s => s.AdmissionNo.Length).ThenBy(s => s.AdmissionNo, StringComparer.Ordinal).ThenBy(s => s.Id).ToList();
        var index = ordered.FindIndex(s => s.Id == studentId);
        return index < 0 ? 1 : index + 1;
    }

    /// <summary>Active students whose guardian's CNIC matches a current staff member of the same campus, without a StaffChild concession.</summary>
    public async Task<List<StaffChildSuggestionDto>> StaffChildSuggestionsAsync(int campusId, int sessionId, CancellationToken ct = default)
    {
        var rows = await (
            from sg in Db.StudentGuardians
            join st in Db.Staff on sg.Guardian.Cnic equals st.Cnic
            where sg.Guardian.Cnic != null && sg.Guardian.Cnic != "" && !sg.Guardian.IsDeleted
                  && sg.Student.CampusId == campusId && !sg.Student.IsDeleted && sg.Student.Status == StudentStatus.Active
                  && st.CampusId == campusId
                  && (st.Status == StaffStatus.Active || st.Status == StaffStatus.OnLeave || st.Status == StaffStatus.Onboarding)
                  && !Db.StudentConcessions.Any(c => c.StudentId == sg.StudentId && c.AcademicSessionId == sessionId && c.IsActive
                                                     && c.Kind == ConcessionKind.StaffChild)
            select new
            {
                sg.StudentId, Name = sg.Student.FirstName + " " + sg.Student.LastName, StaffId = st.Id,
                StaffName = st.FirstName + " " + st.LastName, Cnic = st.Cnic!,
                ClassName = Db.Enrollments.Where(e => e.StudentId == sg.StudentId && e.AcademicSessionId == sessionId)
                    .Select(e => e.Section.Class.Name + " - " + e.Section.Name).FirstOrDefault(),
            }).ToListAsync(ct);
        return rows.DistinctBy(r => (r.StudentId, r.StaffId))
            .Select(r => new StaffChildSuggestionDto(r.StudentId, r.Name, r.ClassName ?? "-", r.StaffId, r.StaffName, "*****-*******-" + r.Cnic[^1]))
            .OrderBy(r => r.StudentName).ToList();
    }

    /// <summary>True when the student has a non-cancelled challan in the session.</summary>
    public Task<bool> HasVoucherInSessionAsync(int studentId, int sessionId, CancellationToken ct = default) =>
        Db.FeeVouchers.IgnoreQueryFilters().AnyAsync(v => !v.IsDeleted && v.StudentId == studentId && v.AcademicSessionId == sessionId
                                                         && v.Status != VoucherStatus.Cancelled, ct);
}
