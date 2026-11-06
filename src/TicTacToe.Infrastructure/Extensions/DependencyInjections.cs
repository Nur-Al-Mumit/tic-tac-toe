using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicTacToe.Application.Common;
using TicTacToe.Infrastructure.Hosting;
using TicTacToe.Infrastructure.Realtime;
using TicTacToe.Application.Contracts;
using TicTacToe.Application.Contracts.Repositories;
using TicTacToe.Application.Contracts.Services;
using TicTacToe.Infrastructure.Data;
using TicTacToe.Infrastructure.Data.Repositories;
using TicTacToe.Infrastructure.Identity;
using TicTacToe.Infrastructure.Services;

namespace TicTacToe.Infrastructure.Extensions;

public static class DependencyInjections
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext(configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured."));
        services.AddIdentityConfiguration(configuration);

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<TicTacToe.Application.Contracts.Services.IPresenceTracker, PresenceTracker>();
        services.AddOptions<GameRulesOptions>().BindConfiguration("GameRules");
        services.AddHostedService<GameTimeoutService>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IApplicationUnitOfWork, ApplicationUnitOfWork>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IPlayerProfileRepository, PlayerProfileRepository>();
        services.AddScoped<IReadDbContext, ReadDbContext>();

        return services;
    }
}
