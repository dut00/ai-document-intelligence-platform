using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIntelligence.Infrastructure.Persistence;

public static class DatabaseMigrator
{
    /// <summary>
    /// Applies pending EF Core migrations. Meant for development and tests; production
    /// deployments should run migrations as a separate step.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
