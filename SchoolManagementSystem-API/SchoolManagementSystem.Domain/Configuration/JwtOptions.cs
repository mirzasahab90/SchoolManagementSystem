namespace SchoolManagementSystem.Domain.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC signing key, at least 32 characters.</summary>
    public string Key { get; set; } = string.Empty;

    public string Issuer { get; set; } = "SchoolManagementSystem";

    public string Audience { get; set; } = "SchoolManagementSystem";

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 7;
}
