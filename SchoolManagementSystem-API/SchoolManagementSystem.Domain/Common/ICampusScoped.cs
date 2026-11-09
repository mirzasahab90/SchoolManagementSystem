namespace SchoolManagementSystem.Domain.Common;

/// <summary>
/// Marks business data that belongs to one campus (students, classes, fees, …).
/// The DbContext then filters reads to the request's campuses and stamps / checks CampusId on save.
/// </summary>
public interface ICampusScoped
{
    int CampusId { get; set; }
}
