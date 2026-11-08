using Microsoft.Extensions.Options;
using SchoolManagementSystem.Domain.Configuration;

namespace SchoolManagementSystem.Api.Infrastructure;

public class RefreshCookie(IOptions<RefreshCookieOptions> options)
{
    private readonly RefreshCookieOptions _options = options.Value;

    public void Set(HttpResponse response, string refreshToken, DateTime expiresUtc)
    {
        response.Cookies.Append(_options.Name, refreshToken, Build(expiresUtc));
    }

    public void Clear(HttpResponse response)
    {
        response.Cookies.Delete(_options.Name, Build(DateTime.UtcNow.AddDays(-1)));
    }

    public string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(_options.Name, out var value) ? value : null;

    private CookieOptions Build(DateTime expiresUtc) => new()
    {
        HttpOnly = true,
        Secure = _options.Secure,
        SameSite = Enum.TryParse<SameSiteMode>(_options.SameSite, true, out var mode) ? mode : SameSiteMode.Lax,
        Path = _options.Path ?? "/",
        Expires = expiresUtc
    };
}
