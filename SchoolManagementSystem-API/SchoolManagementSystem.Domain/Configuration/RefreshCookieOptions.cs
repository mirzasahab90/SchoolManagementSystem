namespace SchoolManagementSystem.Domain.Configuration;

public class RefreshCookieOptions
{
    public const string SectionName = "RefreshCookie";

    public string Name { get; set; } = "sms_refresh";

    public bool Secure { get; set; } = false;

    public string SameSite { get; set; } = "Lax";

    public string? Path { get; set; } = "/api/auth";
}
