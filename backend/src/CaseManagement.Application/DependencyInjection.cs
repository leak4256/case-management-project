using CaseManagement.Application.Interfaces;
using CaseManagement.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CaseManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICaseService, CaseService>();

        return services;
    }
}
