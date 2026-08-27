using CaseManagement.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CaseManagement.Api.Tests.Fixtures;

/// <summary>Hosts the real application in memory, pointed at the test container's database.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Frozen well after the seed dates, so an updated row is unmistakably newer.</summary>
    public static readonly DateTime UtcNow = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    public ApiFactory(string connectionString)
    {
        // A source added from ConfigureWebHost arrives after the builder has read the connection
        // string, and loses to the empty placeholder in appsettings.json.
        Environment.SetEnvironmentVariable(
            $"ConnectionStrings__{DependencyInjection.ConnectionStringName}",
            connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Registered after the application's own TryAdd, so this is the instance that resolves.
        builder.ConfigureServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(UtcNow)));
    }
}
