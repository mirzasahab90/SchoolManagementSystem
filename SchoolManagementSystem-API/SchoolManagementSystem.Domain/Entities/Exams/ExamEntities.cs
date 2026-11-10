using SchoolManagementSystem.Domain.Common;
using SchoolManagementSystem.Domain.Entities.Academics;
using SchoolManagementSystem.Domain.Entities.Org;
using SchoolManagementSystem.Domain.Entities.Students;

namespace SchoolManagementSystem.Domain.Entities.Exams;

public enum ExamType { Monthly, MidTerm, Final, Other }

public enum ExamStatus { Draft, Open, Closed }

public enum PaperSectionStatus { NotStarted, InProgress, Submitted }

public enum MarkStatus { Present, Absent, Exempted }

/// <summary>Grade bands; CampusId null = the Main Campus default, a campus row overrides it for that campus.</summary>
public class GradeScale : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<GradeBand> Bands { get; set; } = new List<GradeBand>();
}

public class GradeBand : Entity
{
    public int GradeScaleId { get; set; }
    public GradeScale GradeScale { get; set; } = null!;
    public string Grade { get; set; } = string.Empty;
    public decimal MinPercent { get; set; }
    public bool IsFail { get; set; }
}

/// <summary>Annual weightage per exam type; resolved campus+session+class → … → Main Campus default.</summary>
public class ResultScheme : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public int? AcademicSessionId { get; set; }
    public int? ClassId { get; set; }
    public int MonthlyWeight { get; set; }
    public int MidTermWeight { get; set; }
    public int FinalWeight { get; set; }
    public int OtherWeight { get; set; }
    public bool MustPassFinal { get; set; }
}

/// <summary>An exam in a session; CampusId null = a Main Campus exam every campus sits.</summary>
public class Exam : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public Campus? Campus { get; set; }
    public int AcademicSessionId { get; set; }
    public AcademicSession AcademicSession { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public ExamType Type { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ExamStatus Status { get; set; } = ExamStatus.Draft;
    public ICollection<ExamPaper> Papers { get; set; } = new List<ExamPaper>();
}

/// <summary>One class's paper for one subject in an exam (owner = the exam's owner).</summary>
public class ExamPaper : AuditableEntity, ICampusOwnable
{
    public int? CampusId { get; set; }
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public decimal TotalMarks { get; set; }
    public decimal PassMarks { get; set; }
    public bool HasPractical { get; set; }
    public decimal? PracticalMarks { get; set; }
    public DateOnly? PaperDate { get; set; }
    public bool IsActive { get; set; } = true;

    public decimal TheoryMarks => HasPractical && PracticalMarks is decimal p ? TotalMarks - p : TotalMarks;
}

/// <summary>Entry progress of a paper in one section.</summary>
public class PaperSection : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int ExamPaperId { get; set; }
    public ExamPaper ExamPaper { get; set; } = null!;
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public PaperSectionStatus Status { get; set; } = PaperSectionStatus.NotStarted;
    public int? SubmittedByUserId { get; set; }
    public DateTime? SubmittedOn { get; set; }
}

/// <summary>A student's marks in a paper, kept with the section the student sat in.</summary>
public class Mark : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int ExamPaperId { get; set; }
    public ExamPaper ExamPaper { get; set; } = null!;
    public int SectionId { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public decimal? Theory { get; set; }
    public decimal? Practical { get; set; }
    public MarkStatus Status { get; set; }
}

public class SectionResult : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public bool IsPublished { get; set; }
    public DateTime? PublishedOn { get; set; }
    public int? PublishedByUserId { get; set; }
}

/// <summary>Snapshot of a student's exam result written on publish (removed on unpublish).</summary>
public class StudentResult : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int ExamId { get; set; }
    public int SectionId { get; set; }
    public int StudentId { get; set; }
    public decimal? Obtained { get; set; }
    public decimal? Total { get; set; }
    public decimal? Percent { get; set; }
    public string? Grade { get; set; }
    public bool? Passed { get; set; }
    public int? Position { get; set; }
    public string SubjectsJson { get; set; } = "[]";
}

public class ResultRemark : AuditableEntity, ICampusScoped
{
    public int CampusId { get; set; }
    public int? ExamId { get; set; }
    public int AcademicSessionId { get; set; }
    public int StudentId { get; set; }
    public string Remark { get; set; } = string.Empty;
}
