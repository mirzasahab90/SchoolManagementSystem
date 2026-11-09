namespace SchoolManagementSystem.Domain.Common;

/// <summary>
/// Data that is either a head-office default (<see cref="CampusId"/> null, visible to every campus)
/// or owned by one campus. The DbContext filters reads and guards writes accordingly.
/// </summary>
public interface ICampusOwnable
{
    int? CampusId { get; set; }
}
