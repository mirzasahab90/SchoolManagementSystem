using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using SchoolManagementSystem.Application.Interfaces;
using SchoolManagementSystem.Application.Repository;

namespace SchoolManagementSystem.Application.Utilities.Security;

/// <summary>
/// Caches each user's module/action grants. Registered as a singleton so <see cref="Invalidate"/>
/// clears the cache for every user, not just the current request.
/// </summary>
public class PermissionChecker(IServiceScopeFactory scopeFactory, IMemoryCache cache) : IPermissionChecker
{
    private CancellationTokenSource _reset = new();

    public async Task<bool> HasPermissionAsync(int userId, string moduleKey, string action, CancellationToken ct = default)
    {
        var access = await cache.GetOrCreateAsync($"access:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            entry.AddExpirationToken(new CancellationChangeToken(_reset.Token));

            await using var scope = scopeFactory.CreateAsyncScope();
            var repositories = scope.ServiceProvider.GetRequiredService<SchoolManagementSystemRepositories>();
            return await repositories.Access.GetUserAccessAsync(userId, CancellationToken.None);
        });

        return access!.Can(moduleKey, action);
    }

    public void Invalidate()
    {
        var previous = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }
}
