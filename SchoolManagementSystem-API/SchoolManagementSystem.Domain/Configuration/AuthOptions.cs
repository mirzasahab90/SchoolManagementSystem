namespace SchoolManagementSystem.Domain.Configuration;

public class AuthOptions
{
    public const string SectionName = "Auth";

    public int MaxFailedAttempts { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 15;
}
