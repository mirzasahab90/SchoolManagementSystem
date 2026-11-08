using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Domain.Entities.Org;

public class UserCampus
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CampusId { get; set; }
    public Campus Campus { get; set; } = null!;
    public bool IsPrimary { get; set; }
}
