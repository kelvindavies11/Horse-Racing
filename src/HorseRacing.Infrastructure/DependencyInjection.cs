using HorseRacing.Application.Abstractions;
using HorseRacing.Infrastructure.Persistence;
using HorseRacing.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HorseRacing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HorseRacing");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'HorseRacing' is not configured.");
        }

        services.AddDbContext<HorseRacingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IRaceRepository, RaceRepository>();

        return services;
    }
}
