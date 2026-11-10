using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Domain.Entities.Hiring;

public enum StaffDesignation { Teacher, Principal, Coordinator, Accountant, Clerk, Librarian, Driver, Guard, Cleaner, Other }

public enum StaffCategory { Teaching, NonTeaching }

public enum VacancyStatus { Open, OnHold, Closed, Filled }

public enum CandidateStage { Applied, Shortlisted, Interview, Offered, Hired, Rejected, Withdrawn }

public enum CandidateSource { WalkIn, Referral, Email, JobPortal, Other }

public enum InterviewResult { Scheduled, Pass, Fail, Hold }

public enum EmploymentType { Permanent, Contract, Visiting, PartTime }

public enum OfferStatus { Pending, Accepted, Declined, Withdrawn }

public enum StaffStatus { Onboarding, Active, OnLeave, Resigned, Terminated }

public enum StaffDocumentType { Cnic, Degree, ExperienceLetter, Contract, Photo, Other }

/// <summary>An open position on one campus.</summary>
public class Vacancy : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public StaffDesignation Designation { get; set; }
    public StaffCategory Category { get; set; }
    public int Positions { get; set; } = 1;

    /// <summary>Comma-separated class / subject ids, for display and filtering only.</summary>
    public string? ClassIds { get; set; }
    public string? SubjectIds { get; set; }

    public string? Requirements { get; set; }
    public DateOnly? ClosingDate { get; set; }
    public VacancyStatus Status { get; set; } = VacancyStatus.Open;

    public ICollection<Candidate> Candidates { get; set; } = new List<Candidate>();
}

/// <summary>A person applying for a vacancy; moves through the hiring stages.</summary>
public class Candidate : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;
    public int VacancyId { get; set; }
    public Vacancy Vacancy { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;

    /// <summary>Digits only.</summary>
    public string? Cnic { get; set; }

    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Qualification { get; set; }
    public decimal? ExperienceYears { get; set; }
    public decimal? CurrentSalary { get; set; }
    public decimal? ExpectedSalary { get; set; }
    public CandidateSource Source { get; set; }
    public string? CvPath { get; set; }
    public string? CvFileName { get; set; }
    public CandidateStage Stage { get; set; } = CandidateStage.Applied;
    public string? StageReason { get; set; }
    public int? StaffId { get; set; }

    public ICollection<InterviewRound> Interviews { get; set; } = new List<InterviewRound>();
    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
}

/// <summary>Append-only record of stage changes.</summary>
public class CandidateStageHistory : Entity
{
    public int CandidateId { get; set; }
    public CandidateStage? FromStage { get; set; }
    public CandidateStage ToStage { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredOn { get; set; }
    public int? ByUserId { get; set; }
}

public class InterviewRound : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public int InterviewerUserId { get; set; }
    public User Interviewer { get; set; } = null!;
    public InterviewResult Result { get; set; } = InterviewResult.Scheduled;
    public int? Rating { get; set; }
    public string? Notes { get; set; }
}

public class Offer : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public StaffDesignation Designation { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public decimal OfferedSalary { get; set; }
    public DateOnly ProposedJoiningDate { get; set; }
    public OfferStatus Status { get; set; } = OfferStatus.Pending;
    public DateTime? RespondedOn { get; set; }
}

/// <summary>A staff member (basic record in Part 1; onboarding fields come in Part 2).</summary>
public class Staff : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;
    public string EmployeeNo { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Cnic { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public StaffCategory Category { get; set; }
    public StaffDesignation Designation { get; set; }
    public EmploymentType EmploymentType { get; set; }
    public DateOnly JoiningDate { get; set; }
    public decimal BasicSalary { get; set; }
    public StaffStatus Status { get; set; } = StaffStatus.Onboarding;
    public int? SourceCandidateId { get; set; }
    public Candidate? SourceCandidate { get; set; }

    // Part 2: profile
    public Gender? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string? PhotoPath { get; set; }
    public string? EmergencyName { get; set; }
    public string? EmergencyRelation { get; set; }
    public string? EmergencyPhone { get; set; }
    public string? Qualification { get; set; }
    public decimal? ExperienceYears { get; set; }
    public string? PreviousEmployer { get; set; }
    public string? BankName { get; set; }

    /// <summary>Account number or IBAN, stored as typed (spaces removed). Shown masked everywhere but the edit form.</summary>
    public string? BankAccount { get; set; }

    // Part 2: login and leaving
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateOnly? LeftOn { get; set; }
    public string? LeaveReason { get; set; }

    // Part 2: manual checklist items
    public bool ContractSigned { get; set; }
    public DateTime? ContractSignedOn { get; set; }
    public bool OrientationDone { get; set; }
    public DateTime? OrientationDoneOn { get; set; }

    public ICollection<StaffDocument> Documents { get; set; } = new List<StaffDocument>();
    public ICollection<TeachingAssignment> Assignments { get; set; } = new List<TeachingAssignment>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class StaffDocument : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public StaffDocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StoredPath { get; set; } = string.Empty;
    public DateTime UploadedOn { get; set; }
    public int? UploadedBy { get; set; }
}

/// <summary>A teacher teaching one subject in one section. Ended rows (EndedOn set) are history.</summary>
public class TeachingAssignment : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int StaffId { get; set; }
    public Staff Staff { get; set; } = null!;
    public int AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public DateOnly StartedOn { get; set; }
    public DateOnly? EndedOn { get; set; }
    public string? EndReason { get; set; }
}
