using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolManagementSystem.Domain.Configuration;
using SchoolManagementSystem.Infrastructure;
using SchoolManagementSystem.Infrastructure.Seeding;

namespace SchoolManagementSystem.Api;

/// <summary>
/// On startup: apply pending migrations (you only create them), then run the idempotent seeder.
/// Same pattern as ResourceHCM — schema updates land automatically after each new migration version.
/// </summary>
public class DatabaseInitializerService(
    IServiceScopeFactory scopeFactory,
    IOptions<DatabaseOptions> databaseOptions,
    ILogger<DatabaseInitializerService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = databaseOptions.Value;
        if (!options.RunMigrationsOnStartup && !options.RunSeederOnStartup)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();

            if (options.RunMigrationsOnStartup)
            {
                var db = scope.ServiceProvider.GetRequiredService<SchoolManagementSystemDbContext>();
                logger.LogInformation("Applying pending database migrations…");
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations are up to date.");
            }

            if (options.RunSeederOnStartup)
            {
                var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
                await seeder.SeedAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Database initialisation failed (migrate/seed).");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
