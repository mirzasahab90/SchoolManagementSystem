namespace SchoolManagementSystem.Domain.Entities.Identity;

public class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public DateTime AssignedOn { get; set; }
    public int? AssignedBy { get; set; }
}
