using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Org;

namespace SchoolManagementSystem.Domain.Entities.Students;

public enum Gender { Male, Female, Other }

public enum StudentStatus { Active, Left }

public enum GuardianRelation { Father, Mother, Guardian, Other }

public enum EnrollmentStatus { Active, Left }

public enum EnrollmentAction { Admitted, SectionChanged, Left, Rejoined }

public enum DocumentType { BForm, BirthCertificate, PreviousResult, LeavingCertificate, Other }

/// <summary>A student; <see cref="CampusId"/> is the student's current campus.</summary>
public class Student : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;

    public string AdmissionNo { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? BFormNo { get; set; }
    public string? BloodGroup { get; set; }
    public string? Religion { get; set; }
    public string? Nationality { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public DateOnly AdmissionDate { get; set; }
    public string? PreviousSchool { get; set; }
    public string? Notes { get; set; }
    public string? PhotoPath { get; set; }
    public StudentStatus Status { get; set; } = StudentStatus.Active;
    public DateOnly? LeftOn { get; set; }
    public string? LeaveReason { get; set; }

    public ICollection<StudentGuardian> Guardians { get; set; } = new List<StudentGuardian>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public ICollection<StudentDocument> Documents { get; set; } = new List<StudentDocument>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

/// <summary>A parent or guardian. School-wide: one guardian may have children on several campuses.</summary>
public class Guardian : AuditableEntity
{
    public string FullName { get; set; } = string.Empty;

    /// <summary>Digits only.</summary>
    public string? Cnic { get; set; }

    public string Phone { get; set; } = string.Empty;
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? Occupation { get; set; }
    public string? Address { get; set; }

    public ICollection<StudentGuardian> Students { get; set; } = new List<StudentGuardian>();
}

public class StudentGuardian
{
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int GuardianId { get; set; }
    public Guardian Guardian { get; set; } = null!;
    public GuardianRelation Relation { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsEmergencyContact { get; set; }
}

/// <summary>A student's place in one session: section, roll number and status.</summary>
public class Enrollment : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public int RollNo { get; set; }
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

/// <summary>Append-only record of enrollment changes (admission, section change, leave, rejoin).</summary>
public class EnrollmentHistory : Entity
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public EnrollmentAction Action { get; set; }
    public int? FromSectionId { get; set; }
    public int? ToSectionId { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredOn { get; set; }
    public int? ByUserId { get; set; }
}

/// <summary>An uploaded file for a student. CampusId mirrors the student's so the campus filter applies.</summary>
public class StudentDocument : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoredPath { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public int? UploadedBy { get; set; }
}

/// <summary>Per-campus counters, e.g. the next admission number for "ADM-2026".</summary>
public class CampusSequence
{
    public int CampusId { get; set; }
    public string Key { get; set; } = string.Empty;
    public int NextValue { get; set; }
}
