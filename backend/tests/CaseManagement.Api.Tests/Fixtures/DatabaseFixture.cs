using CaseManagement.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace CaseManagement.Api.Tests.Fixtures;

/// <summary>
/// One SQL Server container and one API host for the whole test run — a container per test class
/// would cost close to a minute each.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private const string DatabaseName = "CaseManagementTests";

    private readonly MsSqlContainer container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private ApiFactory factory = null!;

    private string connectionString = string.Empty;

    public HttpClient CreateClient() => factory.CreateClient();

    public CaseManagementDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CaseManagementDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new CaseManagementDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        connectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = DatabaseName
        }.ConnectionString;

        // The application seeds 10,000 rows at startup unless the table already holds some, so the
        // small deterministic set has to be in place before the host is built.
        await using (var dbContext = CreateDbContext())
        {
            await dbContext.Database.MigrateAsync();
            dbContext.Cases.AddRange(TestCases.Build());
            await dbContext.SaveChangesAsync();
        }

        factory = new ApiFactory(connectionString);

        // Builds the host here, so a startup failure is reported by the fixture and not by the
        // first test that happens to run.
        _ = factory.Services;
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await container.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "Api";
}
