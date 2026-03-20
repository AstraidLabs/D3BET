using BettingApp.Application.Abstractions;
using BettingApp.Infrastructure.Persistence;
using BettingApp.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BettingApp.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string databasePath)
    {
        services.AddDbContext<BettingDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        services.AddScoped<DatabaseInitializationService>();
        services.AddScoped<IBettingRepository, EfBettingRepository>();
        services.AddScoped<IBettingNotifier, SignalRBettingNotifier>();

        return services;
    }
}
