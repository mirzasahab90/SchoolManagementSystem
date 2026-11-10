using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Domain.Entities.Fees;

public enum FeeFrequency { Monthly, OneTime }

public enum ConcessionKind { Manual, StaffChild }

public enum ConcessionValueType { Percent, Fixed }

public enum VoucherStatus { Unpaid, PartiallyPaid, Paid, CarriedForward, Cancelled }

public enum VoucherLineKind { Head, Arrears, LateFine, Adjustment }

public enum PaymentMethod { Cash, Bank, Online }

/// <summary>A fee head (Tuition, Admission, …); CampusId null = Main Campus head used by every campus.</summary>
public class FeeHead : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    public string Name { get; set; } = string.Empty;
    public FeeFrequency Frequency { get; set; }

    /// <summary>OneTime heads: month 1–12 it is charged in; null = on the first challan after admission.</summary>
    public int? OneTimeMonth { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Amount of a head for a class in a session; a campus row overrides the Main Campus row.</summary>
public class FeeStructure : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public int AcademicSessionId { get; set; }
    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public int FeeHeadId { get; set; }
    public FeeHead FeeHead { get; set; } = null!;
    public decimal Amount { get; set; }
}

/// <summary>Due day, fine, discounts and bank details; campus fields override the Main Campus row where set.</summary>
public class FeeSettings : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public int? DueDay { get; set; }
    public decimal? LateFine { get; set; }
    public decimal? SiblingSecondPercent { get; set; }
    public decimal? SiblingThirdPlusPercent { get; set; }
    public decimal? StaffChildPercent { get; set; }

    /// <summary>Comma list of head ids sibling / staff-child discounts apply to.</summary>
    public string? ConcessionHeadIds { get; set; }

    public string? BankName { get; set; }
    public string? AccountTitle { get; set; }
    public string? AccountNo { get; set; }
    public string? ChallanNote { get; set; }
}

public class StudentConcession : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int AcademicSessionId { get; set; }

    /// <summary>Null = every Monthly head.</summary>
    public int? FeeHeadId { get; set; }

    public FeeHead? FeeHead { get; set; }
    public ConcessionKind Kind { get; set; }
    public int? StaffId { get; set; }
    public ConcessionValueType ValueType { get; set; }
    public decimal Value { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>A student's challan for one month.</summary>
public class FeeVoucher : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;
    public int AcademicSessionId { get; set; }

    /// <summary>First day of the month billed.</summary>
    public DateOnly Month { get; set; }

    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal Total { get; set; }
    public decimal LateFineAmount { get; set; }
    public bool LateFineApplied { get; set; }
    public decimal Paid { get; set; }
    public VoucherStatus Status { get; set; } = VoucherStatus.Unpaid;
    public int? CarriedToVoucherId { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>Optimistic concurrency: two saves on the same challan (payments, carry-forward) cannot both win.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ICollection<FeeVoucherLine> Lines { get; set; } = new List<FeeVoucherLine>();
    public ICollection<FeePayment> Payments { get; set; } = new List<FeePayment>();

    public decimal Balance => Total - Paid;
    public bool IsOpen => Status is VoucherStatus.Unpaid or VoucherStatus.PartiallyPaid;
}

public class FeeVoucherLine : Entity
{
    public int FeeVoucherId { get; set; }
    public FeeVoucher FeeVoucher { get; set; } = null!;
    public VoucherLineKind Kind { get; set; }
    public int? FeeHeadId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Concession { get; set; }
    public string? ConcessionNote { get; set; }
    public decimal Net { get; set; }
    public int? FromVoucherId { get; set; }
}

public class FeePayment : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int FeeVoucherId { get; set; }
    public FeeVoucher FeeVoucher { get; set; } = null!;
    public string ReceiptNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public int ReceivedByUserId { get; set; }
    public bool IsVoid { get; set; }
    public string? VoidReason { get; set; }
    public DateTime? VoidedOn { get; set; }
    public int? VoidedByUserId { get; set; }

    /// <summary>True when this payment added the late fine line (void leaves the line in place).</summary>
    public bool AddedLateFine { get; set; }
}
