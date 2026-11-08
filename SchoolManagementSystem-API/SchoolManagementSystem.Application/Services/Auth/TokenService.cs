using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolManagementSystem.Application.Repository;
using SchoolManagementSystem.Application.Utilities.Security;
using SchoolManagementSystem.Domain.Abstractions;
using SchoolManagementSystem.Domain.Configuration;
using SchoolManagementSystem.Domain.DTOs.Auth;
using SchoolManagementSystem.Domain.Entities.Identity;

namespace SchoolManagementSystem.Application.Services.Auth;

public class TokenService(
    SchoolManagementSystemRepositories repositories,
    IOptions<JwtOptions> jwtOptions,
    ICurrentUser currentUser)
{
    private const int RotationGraceSeconds = 60;
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<TokenResult> IssueAsync(User user, IReadOnlyList<string> roles, CancellationToken cancellationToken)
    {
        var accessExpires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("hq", user.IsHq ? "1" : "0"),
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: accessExpires,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        var refreshToken = SecureToken.Create();
        var refreshExpires = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);
        repositories.Tokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = SecureToken.Hash(refreshToken),
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = refreshExpires,
            CreatedByIp = currentUser.IpAddress
        });
        await repositories.SaveChangesAsync(cancellationToken);

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), accessExpires, refreshToken, refreshExpires);
    }

    public async Task<User?> ConsumeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await repositories.Tokens.GetRefreshTokenAsync(SecureToken.Hash(refreshToken), cancellationToken);
        if (stored == null)
        {
            return null;
        }

        if (stored.RevokedUtc != null)
        {
            if (stored.RevokedUtc < DateTime.UtcNow.AddSeconds(-RotationGraceSeconds))
            {
                await RevokeAllAsync(stored.UserId, cancellationToken);
            }

            return null;
        }

        if (!stored.IsActive || !stored.User.IsActive)
        {
            return null;
        }

        stored.RevokedUtc = DateTime.UtcNow;
        await repositories.SaveChangesAsync(cancellationToken);
        return stored.User;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await repositories.Tokens.GetUnrevokedRefreshTokenAsync(SecureToken.Hash(refreshToken), cancellationToken);
        if (stored == null)
        {
            return;
        }

        stored.RevokedUtc = DateTime.UtcNow;
        await repositories.SaveChangesAsync(cancellationToken);
    }

    public Task RevokeAllAsync(int userId, CancellationToken cancellationToken) =>
        repositories.Tokens.RevokeAllRefreshTokensAsync(userId, DateTime.UtcNow, cancellationToken);
}
