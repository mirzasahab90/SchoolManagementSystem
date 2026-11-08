using Microsoft.EntityFrameworkCore;
using SchoolManagementSystem.Domain.Entities.Identity;
using SchoolManagementSystem.Infrastructure;

namespace SchoolManagementSystem.Application.Repository;

public class TokenRepository(SchoolManagementSystemDbContext dbContext) : BaseRepository(dbContext)
{
    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken ct = default) =>
        Db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<RefreshToken?> GetUnrevokedRefreshTokenAsync(string tokenHash, CancellationToken ct = default) =>
        Db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedUtc == null, ct);

    public async Task RevokeAllRefreshTokensAsync(int userId, DateTime revokedUtc, CancellationToken ct = default)
    {
        var tokens = await Db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedUtc == null).ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.RevokedUtc = revokedUtc;
        }
    }
}
