using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Infrastructure.Common;
using TicTacToe.Infrastructure.Persistence;

namespace TicTacToe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default"))
                   .UseSnakeCaseNamingConvention());

        return services;
    }
}
