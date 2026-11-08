using SchoolManagementSystem.Domain.Common;

namespace SchoolManagementSystem.Domain.Entities.Identity;

public class RefreshToken : Entity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsActive => RevokedUtc == null && DateTime.UtcNow < ExpiresUtc;
}
