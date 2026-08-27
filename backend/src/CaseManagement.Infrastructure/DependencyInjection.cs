using CaseManagement.Application.Interfaces;
using CaseManagement.Infrastructure.Persistence;
using CaseManagement.Infrastructure.Repositories;
using CaseManagement.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CaseManagement.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "CaseManagementDb";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                "Set it with user secrets for local development, or with the " +
                $"ConnectionStrings__{ConnectionStringName} environment variable when running " +
                "in Docker. See README.md for the exact commands.");
        }

        services.AddDbContext<CaseManagementDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        // TryAdd, so a test host that already registered a fake clock keeps it.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ICaseRepository, CaseRepository>();

        services.AddScoped<DatabaseInitializer>();

        return services;
    }
}
