using Microsoft.Extensions.DependencyInjection;
using TicTacToe.Application.Common.Interfaces;
using TicTacToe.Infrastructure.Common;

namespace TicTacToe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        return services;
    }
}
