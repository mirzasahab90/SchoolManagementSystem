namespace SchoolManagementSystem.Domain.Entities.Academics;

/// <summary>A subject taught in a class. The whole list of a class is replaced on save.</summary>
public class ClassSubject
{
    public int ClassId { get; set; }
    public SchoolClass Class { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public bool IsOptional { get; set; }
    public int DisplayOrder { get; set; }
}
