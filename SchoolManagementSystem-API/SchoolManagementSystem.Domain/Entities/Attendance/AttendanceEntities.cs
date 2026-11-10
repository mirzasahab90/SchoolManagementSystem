using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Hiring;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Domain.Entities.Attendance;

public enum StudentAttendanceStatus { Present, Absent, Late, Leave }

public enum StaffAttendanceStatus { Present, Absent, Late, Leave, HalfDay }

public enum DayKind { Working, WeeklyOff, Holiday, Future, OutsideSession }

/// <summary>A campus's weekly off days; one row per campus, created on first change.</summary>
public class CampusCalendar : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;

    /// <summary>Comma-separated DayOfWeek names, e.g. "Sunday".</summary>
    public string WeeklyOff { get; set; } = "Sunday";
}

/// <summary>A holiday range; CampusId null = every campus (Main Campus only).</summary>
public class Holiday : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

/// <summary>One section's attendance on one date; its entries hold each enrolled student's status.</summary>
public class AttendanceDay : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public int AcademicSessionId { get; set; }
    public DateOnly Date { get; set; }
    public int? MarkedByUserId { get; set; }
    public DateTime MarkedOn { get; set; }
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
    public int Leave { get; set; }
    public ICollection<AttendanceEntry> Entries { get; set; } = new List<AttendanceEntry>();
}

/// <summary>A student's status on a day; reached only through its day.</summary>
public class AttendanceEntry : Entity
{
    public int AttendanceDayId { get; set; }
    public AttendanceDay AttendanceDay { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public StudentAttendanceStatus Status { get; set; }
    public string? Note { get; set; }
}

public class StaffAttendance : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public DateOnly Date { get; set; }
    public StaffAttendanceStatus Status { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public string? Note { get; set; }
    public int? MarkedByUserId { get; set; }
}
