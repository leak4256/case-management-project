using System.Diagnostics;
using CaseManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CaseManagement.Infrastructure.Seeding;

public class DatabaseInitializer(
    CaseManagementDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<DatabaseInitializer> logger)
{
    public const int TargetCaseCount = 10_000;

    private const int BatchSize = 1_000;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (await dbContext.Cases.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already contains cases; skipping seed.");
            return;
        }

        await SeedAsync(cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Seeding {Count} cases...", TargetCaseCount);
        var stopwatch = Stopwatch.StartNew();

        var cases = CaseFaker.Generate(TargetCaseCount, timeProvider.GetUtcNow().UtcDateTime);

        // Change detection rescans every tracked entity on each Add, making bulk inserts quadratic.
        var originalAutoDetect = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            foreach (var batch in cases.Chunk(BatchSize))
            {
                dbContext.Cases.AddRange(batch);
                await dbContext.SaveChangesAsync(cancellationToken);

                // This scope is long-lived, so nothing else discards the tracker.
                dbContext.ChangeTracker.Clear();
            }
        }
        finally
        {
            // Left off, later saves on this DbContext would silently do nothing.
            dbContext.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetect;
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Seeded {Count} cases in {ElapsedMs} ms.",
            TargetCaseCount,
            stopwatch.ElapsedMilliseconds);
    }
}
